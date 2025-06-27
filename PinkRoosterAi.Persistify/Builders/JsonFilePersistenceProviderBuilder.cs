using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders;

public class JsonFilePersistenceProviderBuilder<TValue>
    : IPersistenceProviderBuilder<TValue>, IPersistenceProviderBuilder
{
    private TimeSpan _batchInterval = TimeSpan.Zero;
    private int _batchSize = 1;

    private bool _built;
    private string? _filePath;
    private int _maxRetryAttempts = 3;
    private TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);
    private JsonSerializerOptions? _serializerOptions;
    private bool _throwOnPersistenceFailure;

    public IPersistenceProvider<TValue> Build()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder instance has already been used to build a provider. Create a new builder for each provider.");
        }

        if (string.IsNullOrWhiteSpace(_filePath))
        {
            throw new InvalidOperationException("FilePath must be set for JsonFilePersistenceProvider.");
        }

        // Validate file path is not just whitespace and is a valid path
        try
        {
            string fullPath = Path.GetFullPath(_filePath!);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FilePath is not a valid path for JsonFilePersistenceProvider.", ex);
        }

        JsonFilePersistenceOptions options = new JsonFilePersistenceOptions
        {
            FilePath = _filePath!,
            SerializerOptions = _serializerOptions,
            MaxRetryAttempts = _maxRetryAttempts,
            RetryDelay = _retryDelay,
            ThrowOnPersistenceFailure = _throwOnPersistenceFailure,
            BatchSize = _batchSize,
            BatchInterval = _batchInterval
        };
        _built = true;
        var nonGenericProvider = new JsonFilePersistenceProvider(options);
        return new PersistenceProviderAdapter<TValue>(nonGenericProvider);
    }

    IPersistenceProvider IPersistenceProviderBuilder.Build()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder instance has already been used to build a provider. Create a new builder for each provider.");
        }

        if (string.IsNullOrWhiteSpace(_filePath))
        {
            throw new InvalidOperationException("FilePath must be set for JsonFilePersistenceProvider.");
        }

        try
        {
            string fullPath = Path.GetFullPath(_filePath!);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FilePath is not a valid path for JsonFilePersistenceProvider.", ex);
        }

        JsonFilePersistenceOptions options = new JsonFilePersistenceOptions
        {
            FilePath = _filePath!,
            SerializerOptions = _serializerOptions,
            MaxRetryAttempts = _maxRetryAttempts,
            RetryDelay = _retryDelay,
            ThrowOnPersistenceFailure = _throwOnPersistenceFailure,
            BatchSize = _batchSize,
            BatchInterval = _batchInterval
        };
        _built = true;
        return new JsonFilePersistenceProvider(options);
    }

    public JsonFilePersistenceProviderBuilder<TValue> WithFilePath(string path)
    {
        _filePath = path;
        return this;
    }

    public JsonFilePersistenceProviderBuilder<TValue> WithSerializerOptions(JsonSerializerOptions opts)
    {
        _serializerOptions = opts;
        return this;
    }

    public JsonFilePersistenceProviderBuilder<TValue> WithRetry(int maxAttempts, TimeSpan delay)
    {
        _maxRetryAttempts = maxAttempts;
        _retryDelay = delay;
        return this;
    }

    public JsonFilePersistenceProviderBuilder<TValue> ThrowOnFailure(bool yes = true)
    {
        _throwOnPersistenceFailure = yes;
        return this;
    }

    public JsonFilePersistenceProviderBuilder<TValue> WithBatch(int batchSize, TimeSpan batchInterval)
    {
        _batchSize = batchSize;
        _batchInterval = batchInterval;
        return this;
    }
}