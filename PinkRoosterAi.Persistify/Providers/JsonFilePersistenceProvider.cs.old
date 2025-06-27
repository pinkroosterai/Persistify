using System.Text.Json;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
///     Provides JSON file based persistence for PersistentDictionary.
/// </summary>
/// <typeparam name="TValue">The type of dictionary values.</typeparam>
public class JsonFilePersistenceProvider<TValue> : IPersistenceProvider<TValue>,
    IPersistenceMetadataProvider, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonFilePersistenceProvider{TValue}" /> class.
    /// </summary>
    /// <param name="options">The JSON file persistence options.</param>
    public JsonFilePersistenceProvider(JsonFilePersistenceOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(GetFileName(options.FilePath)))
        {
            throw new ArgumentException("FilePath must be specified in JsonFilePersistenceOptions.", nameof(options));
        }

        _serializerOptions = Options.SerializerOptions ?? new JsonSerializerOptions();
    }

    public JsonFilePersistenceOptions Options { get; }
    
    IPersistenceOptions IPersistenceProvider<TValue>.Options => Options;

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    public string GetFileName(string dictionaryName)
    {
        return Path.Combine(Options.FilePath, dictionaryName + ".json");
    }

    public Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(string dictionaryName,CancellationToken ct = default)
    {
        // Approximate: use file's last write time for all keys if file exists, else DateTime.UtcNow
        var dict = new Dictionary<string, DateTime>();
        DateTime updatedAt;
        if (File.Exists(GetFileName(dictionaryName)))
        {
            updatedAt = File.GetLastWriteTimeUtc(GetFileName(dictionaryName));
            // Load keys from file
            using (FileStream stream = new FileStream(GetFileName(dictionaryName), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, TValue>>(stream, _serializerOptions);
                if (data != null)
                {
                    foreach (string key in data.Keys) dict[key] = updatedAt;
                }
            }
        }

        return Task.FromResult(dict);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, TValue>> LoadAsync(string dictionaryName,CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(GetFileName(dictionaryName)))
            {
                return new Dictionary<string, TValue>();
            }

            await using FileStream stream = new FileStream(GetFileName(dictionaryName), FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, true);
            await using BufferedStream buffered = new BufferedStream(stream, 64 * 1024); // 64KB buffer for large files
            var data = await JsonSerializer
                .DeserializeAsync<Dictionary<string, TValue>>(buffered, _serializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return data ?? new Dictionary<string, TValue>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string dictionaryName,Dictionary<string, TValue> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        string tempFilePath = GetFileName(dictionaryName) + ".tmp";
        try
        {
            await using (FileStream tempStream =
                         new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            await using (BufferedStream
                         buffered = new BufferedStream(tempStream, 64 * 1024)) // 64KB buffer for large files
            {
                await JsonSerializer.SerializeAsync(buffered, data, _serializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await buffered.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Replace original file atomically, using a cross-platform safe strategy
            try
            {
                if (File.Exists(GetFileName(dictionaryName)))
                {
                    try
                    {
                        // Try File.Replace if available (Windows)
                        File.Replace(tempFilePath, GetFileName(dictionaryName), null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // Fallback for non-Windows: use File.Copy + File.Delete for cross-volume safety
                        string backupPath = GetFileName(dictionaryName) + ".bak";
                        try
                        {
                            File.Copy(tempFilePath, GetFileName(dictionaryName), true);
                            File.Delete(tempFilePath);
                        }
                        catch
                        {
                            // If copy fails, try to restore from backup if possible
                            if (File.Exists(backupPath))
                            {
                                try
                                {
                                    File.Copy(backupPath, GetFileName(dictionaryName), true);
                                }
                                catch
                                {
                                }

                                try
                                {
                                    File.Delete(backupPath);
                                }
                                catch
                                {
                                }
                            }

                            throw;
                        }
                    }
                }
                else
                {
                    // If the target doesn't exist, just move (rename) the temp file
                    File.Move(tempFilePath, GetFileName(dictionaryName));
                }
            }
            catch
            {
                // Clean up temp file on failure
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }
        finally
        {
            // Always try to clean up temp file if it still exists
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                }
            }

            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string dictionaryName,CancellationToken cancellationToken = default)
    {
        bool exists = File.Exists(GetFileName(dictionaryName));
        return Task.FromResult(exists);
    }

    public PersistentDictionary<TValue> CreateDictionary(string dictionaryName,ILogger<PersistentDictionary<TValue>>? logger = null)
        => logger is null ? new PersistentDictionary<TValue>(this, dictionaryName)
                          : new PersistentDictionary<TValue>(this, dictionaryName, logger);

    public CachingPersistentDictionary<TValue> CreateCachingDictionary(string dictionaryName,TimeSpan ttl,
                          ILogger<PersistentDictionary<TValue>>? logger = null)
        => logger is null ? new CachingPersistentDictionary<TValue>(this, dictionaryName,ttl)
                          : new CachingPersistentDictionary<TValue>(this, dictionaryName,ttl, logger);


}