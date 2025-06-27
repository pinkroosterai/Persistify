using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Events;
using PinkRoosterAi.Persistify.Providers;
using Polly;
using Polly.Retry;

namespace PinkRoosterAi.Persistify;

/// <summary>
///     A thread-safe persistent dictionary that automatically persists changes using a configured persistence provider.
///     Supports batch commits and retry logic with exponential backoff.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <remarks>
///     This class is thread-safe for concurrent reads and writes.
///     All mutation methods are asynchronous and should be awaited to ensure persistence.
///     The dictionary must be initialized by calling <see cref="InitializeAsync"/> before use.
///     Implements <see cref="IDisposable"/> to flush pending changes and dispose the underlying persistence provider.
/// </remarks>
public class PersistentDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable
    where TKey : notnull
{
    protected virtual void OnAccess(TKey key) { }
    protected virtual void OnMutation(TKey key) { }

    private readonly object _syncRoot = new();
    private readonly ILogger<PersistentDictionary<TKey, TValue>>? _logger;
    internal readonly IPersistenceProvider<TKey, TValue> PersistenceProvider;
    private bool _disposed;

    // Async initialization gate
    private readonly SemaphoreSlim _initGate = new(1, 1);

    // Batch commit fields
    private int _pendingCount;
    private readonly object _batchLock = new();
    private System.Timers.Timer? _batchTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PersistentDictionary{TKey, TValue}"/> class with the specified persistence provider.
    /// </summary>
    /// <param name="persistenceProvider">The persistence provider to use for saving and loading data.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="persistenceProvider"/> is null.</exception>
    public PersistentDictionary(IPersistenceProvider<TKey, TValue> persistenceProvider)
    {
        this.PersistenceProvider = persistenceProvider ?? throw new ArgumentNullException(nameof(persistenceProvider));
        var opts = GetPersistenceOptions();
        if (opts?.BatchInterval > TimeSpan.Zero)
        {
            _batchTimer = new System.Timers.Timer(opts.BatchInterval.TotalMilliseconds)
            {
                AutoReset = false
            };
            _batchTimer.Elapsed += async (_, __) => await FlushInternalAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PersistentDictionary{TKey, TValue}"/> class with the specified persistence provider and logger.
    /// </summary>
    /// <param name="persistenceProvider">The persistence provider to use for saving and loading data.</param>
    /// <param name="logger">An optional logger for error reporting.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="persistenceProvider"/> is null.</exception>
    public PersistentDictionary(IPersistenceProvider<TKey, TValue> persistenceProvider,
        ILogger<PersistentDictionary<TKey, TValue>>? logger)
        : this(persistenceProvider)
    {
        _logger = logger;
        var opts = GetPersistenceOptions();
        if (opts?.BatchInterval > TimeSpan.Zero)
        {
            _batchTimer = new System.Timers.Timer(opts.BatchInterval.TotalMilliseconds)
            {
                AutoReset = false
            };
            _batchTimer.Elapsed += async (_, __) => await FlushInternalAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the dictionary has been initialized by loading persisted data.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     Gets or sets the value associated with the specified key.
    ///     Throws <see cref="InvalidOperationException"/> if the dictionary is not initialized.
    ///     Setting a value buffers the change for batch persistence.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public new TValue this[TKey key]
    {
        get
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PersistentDictionary is not initialized.");
            }
            lock (_syncRoot)
            {
                var value = base[key];
                OnAccess(key);
                return value;
            }
        }
        set
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("PersistentDictionary is not initialized.");
            }
            lock (_syncRoot)
            {
                base[key] = value;
            }
            // buffer this change for a batch flush
            TrackMutation();
            OnMutation(key);
        }
    }

    /// <summary>
    ///     Disposes the dictionary, flushing any buffered mutations and disposing the underlying persistence provider if disposable.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Occurs when a persistence error happens during retry attempts.
    /// </summary>
    public event EventHandler<PersistenceErrorEventArgs>? PersistenceError;

    private IPersistenceOptions? GetPersistenceOptions()
    {
        return PersistenceProvider switch
        {
            JsonFilePersistenceProvider<TKey, TValue> jsonProvider => jsonProvider.Options,
            DatabasePersistenceProvider<TKey, TValue> dbProvider => dbProvider.Options,
            _ => null
        };
    }

    /// <summary>
    ///     Executes the given operation with retry logic using Polly, with exponential back-off and jitter to avoid thundering-herd.
    ///     If <see cref="IPersistenceOptions.ThrowOnPersistenceFailure"/> is false and all attempts fail, the last exception is returned for the caller to handle.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="operationName">A name for logging and error events.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     Returns null if the operation succeeded, or the last exception if all attempts failed and <see cref="IPersistenceOptions.ThrowOnPersistenceFailure"/> is false.
    /// </returns>
    /// <exception cref="Exception">Throws if <see cref="IPersistenceOptions.ThrowOnPersistenceFailure"/> is true and all attempts fail.</exception>
    private async Task<Exception?> ExecuteWithRetryAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken = default)
    {
        IPersistenceOptions? options = GetPersistenceOptions();

        int maxAttempts = options?.MaxRetryAttempts ?? 3;
        TimeSpan baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(100);
        bool throwOnFailure = options?.ThrowOnPersistenceFailure ?? false;

        Exception? lastException = null;
        var jitterer = new Random();

        // Polly retry with exponential backoff and jitter
        AsyncRetryPolicy policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxAttempts,
                attempt =>
                {
                    // Exponential backoff with jitter (up to 100ms or baseDelay.TotalMilliseconds/2, whichever is smaller)
                    var exp = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
                    var capped = Math.Min(exp, 60_000); // cap at 1 minute
                    var jitterMax = Math.Min(100, baseDelay.TotalMilliseconds / 2.0);
                    var jitter = jitterer.NextDouble() * jitterMax;
                    return TimeSpan.FromMilliseconds(capped + jitter);
                },
                (exception, timespan, attempt, context) =>
                {
                    lastException = exception;
                    _logger?.LogError(exception, "Persistence operation {Operation} failed on attempt {Attempt}. Retrying in {Delay}ms.", operationName, attempt, timespan.TotalMilliseconds);
                    PersistenceError?.Invoke(this,
                        new PersistenceErrorEventArgs(exception, operationName, attempt, attempt == maxAttempts));
                    return Task.CompletedTask;
                }
            );

        try
        {
            await policy.ExecuteAsync((ct) => operation(), cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
            {
                throw;
            }
            return lastException ?? ex;
        }
    }

    /// <summary>
    ///     Asynchronously initializes the dictionary by loading persisted data.
    ///     This method is thread-safe and can be called concurrently; initialization will only happen once.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if loading or existence check fails.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsInitialized)
            {
                return;
            }

            bool exists = false;
            Dictionary<TKey, TValue>? data = null;

            var existsEx = await ExecuteWithRetryAsync(
                async () =>
                {
                    exists = await PersistenceProvider.ExistsAsync(cancellationToken).ConfigureAwait(false);
                }, "ExistsAsync", cancellationToken);
            if (existsEx != null)
            {
                throw new InvalidOperationException("Failed to check persistence existence.", existsEx);
            }

            if (exists)
            {
                var loadEx = await ExecuteWithRetryAsync(
                    async () =>
                    {
                        data = await PersistenceProvider.LoadAsync(cancellationToken).ConfigureAwait(false);
                    }, "LoadAsync", cancellationToken);
                if (loadEx != null)
                {
                    throw new InvalidOperationException("Failed to load persisted data.", loadEx);
                }
            }

            lock (_syncRoot)
            {
                if (IsInitialized)
                {
                    return;
                }
                base.Clear();
                if (exists && data != null)
                {
                    foreach (var kvp in data)
                        base[kvp.Key] = kvp.Value;
                }
                IsInitialized = true;
            }
        }
        finally
        {
            _initGate.Release();
        }
    }

    /// <summary>
    ///     Adds the specified key and value to the dictionary and immediately persists the change.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous add and save operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public async Task AddAndSaveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        lock (_syncRoot)
        {
            base.Add(key, value);
        }
        // buffer this change for a batch flush
        TrackMutation();
        OnMutation(key);
    }

    /// <summary>
    ///     Removes the value with the specified key from the dictionary and immediately persists the change.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous remove and save operation, returning true if the key was removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public async Task<bool> RemoveAndSaveAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        bool removed;
        lock (_syncRoot)
        {
            removed = base.Remove(key);
        }

        if (removed)
        {
            // buffer this change for a batch flush
            TrackMutation();
            OnMutation(key);
            return true;
        }
        return false;
    }
    




    /// <summary>
    ///     Removes all keys and values from the dictionary and immediately persists the change.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous clear and save operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public async Task ClearAndSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        TKey[] keys;
        lock (_syncRoot)
        {
            keys = this.Keys.ToArray();
            base.Clear();
        }
        // buffer this change for a batch flush
        TrackMutation();
        foreach (var key in keys)
        {
            OnMutation(key);
        }
    }

    /// <summary>
    ///     Attempts to add the specified key and value to the dictionary and immediately persists the change if successful.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous try-add and save operation, returning true if the key was added.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public async Task<bool> TryAddAndSaveAsync(TKey key, TValue value, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        bool added = false;
        lock (_syncRoot)
        {
            if (ContainsKey(key))
            {
                return false;
            }
            base.Add(key, value);
            added = true;
        }

        if (added)
        {
            // buffer this change for a batch flush
            TrackMutation();
            OnMutation(key);
            return true;
        }
        return false;
    }

    /// <summary>
    ///     Attempts to remove the value with the specified key from the dictionary and immediately persists the change if successful.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous try-remove and save operation, returning true if the key was removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized.</exception>
    public async Task<bool> TryRemoveAndSaveAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        bool removed;
        lock (_syncRoot)
        {
            removed = base.Remove(key);
        }

        if (removed)
        {
            // buffer this change for a batch flush
            TrackMutation();
            OnMutation(key);
            return true;
        }
        return false;
    }

    // Synchronous mutation methods (Add, Remove, Clear) are intentionally omitted to encourage async usage.
    // Use AddAndSaveAsync, RemoveAndSaveAsync, ClearAndSaveAsync, etc. for all mutations.

    /// <summary>
    ///     Flushes all buffered mutations to the persistence provider asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized or if flushing fails.</exception>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        Dictionary<TKey, TValue> snapshot;
        lock (_syncRoot)
        {
            snapshot = new Dictionary<TKey, TValue>(this);
        }

        var ex = await ExecuteWithRetryAsync(
            () => PersistenceProvider.SaveAsync(snapshot, cancellationToken),
            "FlushAsync", cancellationToken).ConfigureAwait(false);
        if (ex != null)
        {
            throw new InvalidOperationException("Failed to flush persisted data.", ex);
        }
    }

    /// <summary>
    ///     Reloads the dictionary from the persistence provider asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary is not initialized or if reloading fails.</exception>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<TKey, TValue>? data = null;

        var ex = await ExecuteWithRetryAsync(
            async () => { data = await PersistenceProvider.LoadAsync(cancellationToken).ConfigureAwait(false); },
            "ReloadAsync", cancellationToken).ConfigureAwait(false);

        if (ex != null)
        {
            throw new InvalidOperationException("Failed to reload persisted data.", ex);
        }

        if (!IsInitialized)
        {
            throw new InvalidOperationException("PersistentDictionary is not initialized.");
        }
        lock (_syncRoot)
        {
            base.Clear();
            if (data != null)
            {
                foreach (var kvp in data) base[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    ///     Saves the current dictionary state with retry logic.
    ///     Returns the last exception if all attempts fail, or null on success.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous save operation, returning the last exception on failure or null on success.</returns>
    private Task<Exception?> SaveAsyncSafe(CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(() => PersistenceProvider.SaveAsync(new Dictionary<TKey, TValue>(this), cancellationToken),
            "SaveAsync", cancellationToken);
    }

    /// <summary>
    ///     Disposes the dictionary, flushing any buffered mutations and disposing the underlying persistence provider if disposable.
    /// </summary>
    /// <param name="disposing">True if called from Dispose; false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // flush any buffered mutations before disposing
            if (_pendingCount > 0)
            {
                try
                {
                    FlushAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception during FlushAsync in Dispose.");
                }
            }
            if (_batchTimer != null)
            {
                _batchTimer.Stop();
                _batchTimer.Dispose();
                _batchTimer = null;
            }
            if (PersistenceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
        }

        _disposed = true;
    }

    private async Task FlushInternalAsync()
    {
        // clear count under lock so new mutations re-arm
        lock (_batchLock) { _pendingCount = 0; }
        await FlushAsync().ConfigureAwait(false);
    }

    private void TrackMutation()
    {
        var opts = GetPersistenceOptions();
        if (opts == null) return; // No options available, skip batching
        
        lock (_batchLock)
        {
            _pendingCount++;
            // if we've hit the batch size, flush now
            if (_pendingCount >= opts.BatchSize)
            {
                _batchTimer?.Stop();
                _pendingCount = 0;
                // Fire-and-forget, but log any exceptions
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await FlushAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception during background FlushAsync in TrackMutation.");
                    }
                });
            }
            else
            {
                // re-arm timer if configured
                _batchTimer?.Stop();
                _batchTimer?.Start();
            }
        }
    }
}
