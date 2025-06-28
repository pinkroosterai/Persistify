using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify;

public class CachingPersistentDictionary<TValue> : PersistentDictionary<TValue>, IAsyncDisposable
{
    private readonly object _cacheLock = new object();
    private readonly Dictionary<string, DateTime> _lastReadAt = new Dictionary<string, DateTime>();
    private readonly Dictionary<string, DateTime> _lastUpdatedAt = new Dictionary<string, DateTime>();
    private readonly TimeSpan _ttl;
    private readonly Timer _cleanupTimer;
    private readonly ILogger<PersistentDictionary<TValue>>? _logger;
    private bool _disposed;

    public CachingPersistentDictionary(IPersistenceProvider<TValue> persistenceProvider, string dictionaryName,
        TimeSpan cachedTimeBeforeEviction)
        : base(persistenceProvider, dictionaryName)
    {
        if (cachedTimeBeforeEviction <= TimeSpan.Zero)
            throw new ArgumentException("TTL must be positive.", nameof(cachedTimeBeforeEviction));
            
        _ttl = cachedTimeBeforeEviction;
        _cleanupTimer = new Timer(CleanupCallback, null, _ttl, _ttl);
    }

    public CachingPersistentDictionary(IPersistenceProvider<TValue> persistenceProvider, string dictionaryName,
        TimeSpan cachedTimeBeforeEviction, ILogger<PersistentDictionary<TValue>>? logger)
        : base(persistenceProvider, dictionaryName, logger)
    {
        if (cachedTimeBeforeEviction <= TimeSpan.Zero)
            throw new ArgumentException("TTL must be positive.", nameof(cachedTimeBeforeEviction));
            
        _ttl = cachedTimeBeforeEviction;
        _logger = logger;
        _cleanupTimer = new Timer(CleanupCallback, null, _ttl, _ttl);
    }

    public new async Task InitializeAsync(CancellationToken ct = default)
    {
        await base.InitializeAsync(ct);
        DateTime now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            foreach (string key in Keys)
                _lastReadAt[key] = now;
        }

        if (PersistenceProvider is IPersistenceMetadataProvider metaProvider)
        {
            var updatedDict = await metaProvider.LoadLastUpdatedAsync(DictionaryName, ct).ConfigureAwait(false);
            lock (_cacheLock)
            {
                foreach (var kvp in updatedDict) _lastUpdatedAt[kvp.Key] = kvp.Value;
            }
        }

        EvictExpiredEntries();
    }

    protected override void OnAccess(string key)
    {
        lock (_cacheLock)
        {
            _lastReadAt[key] = DateTime.UtcNow;
        }

        EvictExpiredEntries();
    }

    protected override void OnMutation(string key)
    {
        EvictExpiredEntries();
        lock (_cacheLock)
        {
            _lastReadAt[key] = DateTime.UtcNow;
            _lastUpdatedAt[key] = DateTime.UtcNow;
        }
    }

    private void EvictExpiredEntries()
    {
        DateTime now = DateTime.UtcNow;
        var toRemove = new List<string>();
        lock (_cacheLock)
        {
            foreach (string key in Keys)
            {
                DateTime readAt = _lastReadAt.GetValueOrDefault(key, now);
                DateTime updatedAt = _lastUpdatedAt.GetValueOrDefault(key, now);
                if (now - readAt > _ttl || now - updatedAt > _ttl)
                {
                    toRemove.Add(key);
                }
            }
        }

        if (!toRemove.Any())
        {
            return;
        }

        // Use proper synchronization without reflection
        lock (_cacheLock)
        {
            foreach (string key in toRemove)
            {
                if (ContainsKey(key))
                {
                    Remove(key);
                }
                _lastReadAt.Remove(key);
                _lastUpdatedAt.Remove(key);
            }
        }

        // Handle persistence properly instead of fire-and-forget
        var persistenceTasks = toRemove.Select(async key => 
        {
            try
            {
                await RemoveAndSaveAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but don't rethrow to avoid breaking eviction
                _logger?.LogWarning(ex, "Failed to persist removal of key '{Key}' during eviction", key);
            }
        });
        
        _ = Task.WhenAll(persistenceTasks); // Still fire-and-forget but with proper error handling
    }

    private void CleanupCallback(object? state)
    {
        try
        {
            EvictExpiredEntries();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the timer
            _logger?.LogError(ex, "Error during scheduled cleanup in CachingPersistentDictionary");
        }
    }
    
    public new virtual async Task<bool> RemoveAndSaveAsync(string key, CancellationToken cancellationToken = default)
    {
        var result = await base.RemoveAndSaveAsync(key, cancellationToken).ConfigureAwait(false);
        
        // Clean up tracking metadata when item is removed
        lock (_cacheLock)
        {
            _lastReadAt.Remove(key);
            _lastUpdatedAt.Remove(key);
        }
        
        return result;
    }
    
    public new async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
            
        try
        {
            _cleanupTimer?.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cleanupTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}