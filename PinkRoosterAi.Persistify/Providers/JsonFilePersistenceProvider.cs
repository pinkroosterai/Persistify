using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
///     Provides JSON file based persistence for PersistentDictionary.
/// </summary>
/// <typeparam name="TKey">The type of dictionary keys.</typeparam>
/// <typeparam name="TValue">The type of dictionary values.</typeparam>
public class JsonFilePersistenceProvider<TKey, TValue> : IPersistenceProvider<TKey, TValue>, IPersistenceMetadataProvider<TKey>, IDisposable, IAsyncDisposable
    where TKey : notnull
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonFilePersistenceProvider{TKey, TValue}" /> class.
    /// </summary>
    /// <param name="options">The JSON file persistence options.</param>
    public JsonFilePersistenceProvider(JsonFilePersistenceOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(Options.FilePath))
        {
            throw new ArgumentException("FilePath must be specified in JsonFilePersistenceOptions.", nameof(options));
        }

        _serializerOptions = Options.SerializerOptions ?? new JsonSerializerOptions();
    }

    private readonly JsonSerializerOptions _serializerOptions;

    public JsonFilePersistenceOptions Options { get; }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Dictionary<TKey, TValue>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(Options.FilePath))
            {
                return new Dictionary<TKey, TValue>();
            }

            await using FileStream stream = new FileStream(Options.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            await using var buffered = new BufferedStream(stream, 64 * 1024); // 64KB buffer for large files
            var data = await JsonSerializer
                .DeserializeAsync<Dictionary<TKey, TValue>>(buffered, _serializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return data ?? new Dictionary<TKey, TValue>();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(Dictionary<TKey, TValue> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        string tempFilePath = Options.FilePath + ".tmp";
        try
        {
            await using (FileStream tempStream =
                         new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var buffered = new BufferedStream(tempStream, 64 * 1024)) // 64KB buffer for large files
            {
                await JsonSerializer.SerializeAsync(buffered, data, _serializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await buffered.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Replace original file atomically, using a cross-platform safe strategy
            try
            {
                if (File.Exists(Options.FilePath))
                {
                    try
                    {
                        // Try File.Replace if available (Windows)
                        File.Replace(tempFilePath, Options.FilePath, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // Fallback for non-Windows: use File.Copy + File.Delete for cross-volume safety
                        string backupPath = Options.FilePath + ".bak";
                        try
                        {
                            File.Copy(tempFilePath, Options.FilePath, overwrite: true);
                            File.Delete(tempFilePath);
                        }
                        catch
                        {
                            // If copy fails, try to restore from backup if possible
                            if (File.Exists(backupPath))
                            {
                                try { File.Copy(backupPath, Options.FilePath, overwrite: true); } catch { }
                                try { File.Delete(backupPath); } catch { }
                            }
                            throw;
                        }
                    }
                }
                else
                {
                    // If the target doesn't exist, just move (rename) the temp file
                    File.Move(tempFilePath, Options.FilePath);
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
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        bool exists = File.Exists(Options.FilePath);
        return Task.FromResult(exists);
    }
    public Task<Dictionary<TKey, DateTime>> LoadLastUpdatedAsync(CancellationToken ct = default)
    {
        // Approximate: use file's last write time for all keys if file exists, else DateTime.UtcNow
        var dict = new Dictionary<TKey, DateTime>();
        DateTime updatedAt;
        if (File.Exists(Options.FilePath))
        {
            updatedAt = File.GetLastWriteTimeUtc(Options.FilePath);
            // Load keys from file
            using (var stream = new FileStream(Options.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var data = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(stream, _serializerOptions);
                if (data != null)
                {
                    foreach (var key in data.Keys)
                    {
                        dict[key] = updatedAt;
                    }
                }
            }
        }
        return Task.FromResult(dict);
    }
}
