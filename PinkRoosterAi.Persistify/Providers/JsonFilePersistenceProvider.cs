using System.Text.Json;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
/// JSON file persistence provider that handles different value types at runtime.
/// </summary>
public class JsonFilePersistenceProvider : IPersistenceProvider, IPersistenceProvider<object>, IPersistenceMetadataProvider, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonFilePersistenceProvider(JsonFilePersistenceOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.FilePath))
        {
            throw new ArgumentException("FilePath must be specified in JsonFilePersistenceOptions.", nameof(options));
        }

        _serializerOptions = Options.SerializerOptions ?? new JsonSerializerOptions();
    }

    public JsonFilePersistenceOptions Options { get; }
    
    IPersistenceOptions IPersistenceProvider.Options => Options;
    IPersistenceOptions IPersistenceProvider<object>.Options => Options;

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

    public Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(string dictionaryName, CancellationToken ct = default)
    {
        var dict = new Dictionary<string, DateTime>();
        DateTime updatedAt;
        if (File.Exists(GetFileName(dictionaryName)))
        {
            updatedAt = File.GetLastWriteTimeUtc(GetFileName(dictionaryName));
            // Load keys from file
            using (FileStream stream = new FileStream(GetFileName(dictionaryName), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(stream, _serializerOptions);
                if (data != null)
                {
                    foreach (string key in data.Keys) dict[key] = updatedAt;
                }
            }
        }

        return Task.FromResult(dict);
    }

    public async Task<Dictionary<string, object>> LoadAsync(string dictionaryName, Type valueType, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(GetFileName(dictionaryName)))
            {
                return new Dictionary<string, object>();
            }

            await using FileStream stream = new FileStream(GetFileName(dictionaryName), FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, true);
            await using BufferedStream buffered = new BufferedStream(stream, 64 * 1024);
            
            var jsonData = await JsonSerializer
                .DeserializeAsync<Dictionary<string, JsonElement>>(buffered, _serializerOptions, cancellationToken)
                .ConfigureAwait(false);
            
            var result = new Dictionary<string, object>();
            if (jsonData != null)
            {
                foreach (var kvp in jsonData)
                {
                    try
                    {
                        object value = DeserializeValue(kvp.Value, valueType);
                        result[kvp.Key] = value;
                    }
                    catch (Exception ex)
                    {
                        // Log warning and skip invalid entries - ideally use proper logging
                        Console.WriteLine($"Warning: Failed to deserialize value for key '{kvp.Key}': {ex.Message}");
                    }
                }
            }
            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(string dictionaryName, Type valueType, Dictionary<string, object> data, CancellationToken cancellationToken = default)
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
            await using (BufferedStream buffered = new BufferedStream(tempStream, 64 * 1024))
            {
                // Convert object values to serializable format
                var serializableData = new Dictionary<string, object>();
                foreach (var kvp in data)
                {
                    serializableData[kvp.Key] = SerializeValue(kvp.Value, valueType);
                }
                
                await JsonSerializer.SerializeAsync(buffered, serializableData, _serializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await buffered.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Replace original file atomically
            if (File.Exists(GetFileName(dictionaryName)))
            {
                File.Replace(tempFilePath, GetFileName(dictionaryName), null);
            }
            else
            {
                File.Move(tempFilePath, GetFileName(dictionaryName));
            }
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            throw;
        }
        finally
        {
            // Always try to clean up temp file if it still exists
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            _semaphore.Release();
        }
    }

    public Task<bool> ExistsAsync(string dictionaryName, CancellationToken cancellationToken = default)
    {
        bool exists = File.Exists(GetFileName(dictionaryName));
        return Task.FromResult(exists);
    }

    public PersistentDictionary<TValue> CreateDictionary<TValue>(string dictionaryName, ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        // Create adapter that bridges to the non-generic implementation
        var adapter = new PersistenceProviderAdapter<TValue>(this);
        return logger is null ? new PersistentDictionary<TValue>(adapter, dictionaryName)
                              : new PersistentDictionary<TValue>(adapter, dictionaryName, logger);
    }

    public CachingPersistentDictionary<TValue> CreateCachingDictionary<TValue>(string dictionaryName, TimeSpan ttl,
                      ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        var adapter = new PersistenceProviderAdapter<TValue>(this);
        return logger is null ? new CachingPersistentDictionary<TValue>(adapter, dictionaryName, ttl)
                              : new CachingPersistentDictionary<TValue>(adapter, dictionaryName, ttl, logger);
    }

    private object DeserializeValue(JsonElement jsonElement, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return jsonElement.GetString() ?? string.Empty;
        }
        
        if (targetType == typeof(int))
        {
            return jsonElement.GetInt32();
        }
        
        if (targetType == typeof(long))
        {
            return jsonElement.GetInt64();
        }
        
        if (targetType == typeof(bool))
        {
            return jsonElement.GetBoolean();
        }
        
        if (targetType == typeof(double))
        {
            return jsonElement.GetDouble();
        }
        
        if (targetType == typeof(DateTime))
        {
            return jsonElement.GetDateTime();
        }
        
        // For complex types, deserialize from JSON
        return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType, _serializerOptions) 
               ?? Activator.CreateInstance(targetType) 
               ?? throw new InvalidOperationException($"Cannot create instance of type {targetType}");
    }

    private object SerializeValue(object value, Type valueType)
    {
        if (value == null) return null!;
        
        // For primitive types, return as-is for JSON serialization
        if (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(DateTime))
        {
            return value;
        }
        
        // For complex types, the JsonSerializer will handle it properly
        return value;
    }

    // Legacy support for generic interface
    async Task<Dictionary<string, object>> IPersistenceProvider<object>.LoadAsync(string dictionaryName, CancellationToken cancellationToken)
        => await LoadAsync(dictionaryName, typeof(object), cancellationToken).ConfigureAwait(false);

    async Task IPersistenceProvider<object>.SaveAsync(string dictionaryName, Dictionary<string, object> data, CancellationToken cancellationToken)
        => await SaveAsync(dictionaryName, typeof(object), data, cancellationToken).ConfigureAwait(false);

    PersistentDictionary<object> IPersistenceProvider<object>.CreateDictionary(string dictionaryName, ILogger<PersistentDictionary<object>>? logger)
        => CreateDictionary<object>(dictionaryName, logger);

    CachingPersistentDictionary<object> IPersistenceProvider<object>.CreateCachingDictionary(string dictionaryName, TimeSpan ttl, ILogger<PersistentDictionary<object>>? logger)
        => CreateCachingDictionary<object>(dictionaryName, ttl, logger);
}

/// <summary>
/// Adapter class that allows the non-generic provider to work with the existing generic PersistentDictionary
/// </summary>
internal class PersistenceProviderAdapter<TValue> : IPersistenceProvider<TValue>
{
    private readonly IPersistenceProvider _provider;

    public PersistenceProviderAdapter(IPersistenceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public IPersistenceOptions Options => _provider.Options;

    public async Task<Dictionary<string, TValue>> LoadAsync(string dictionaryName, CancellationToken cancellationToken = default)
    {
        var objectData = await _provider.LoadAsync(dictionaryName, typeof(TValue), cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, TValue>();
        
        foreach (var kvp in objectData)
        {
            if (kvp.Value is TValue value)
            {
                result[kvp.Key] = value;
            }
            else if (kvp.Value != null)
            {
                // Attempt conversion
                try
                {
                    if (typeof(TValue) == typeof(string))
                    {
                        result[kvp.Key] = (TValue)(object)kvp.Value.ToString()!;
                    }
                    else
                    {
                        result[kvp.Key] = (TValue)Convert.ChangeType(kvp.Value, typeof(TValue));
                    }
                }
                catch
                {
                    // Skip invalid entries
                }
            }
        }
        return result;
    }

    public async Task SaveAsync(string dictionaryName, Dictionary<string, TValue> data, CancellationToken cancellationToken = default)
    {
        var objectData = new Dictionary<string, object>();
        foreach (var kvp in data)
        {
            if (kvp.Value != null)
                objectData[kvp.Key] = kvp.Value;
        }
        await _provider.SaveAsync(dictionaryName, typeof(TValue), objectData, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(string dictionaryName, CancellationToken cancellationToken = default)
    {
        return _provider.ExistsAsync(dictionaryName, cancellationToken);
    }

    public PersistentDictionary<TValue> CreateDictionary(string dictionaryName, ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        return _provider.CreateDictionary<TValue>(dictionaryName, logger);
    }

    public CachingPersistentDictionary<TValue> CreateCachingDictionary(string dictionaryName, TimeSpan ttl, ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        return _provider.CreateCachingDictionary<TValue>(dictionaryName, ttl, logger);
    }
}