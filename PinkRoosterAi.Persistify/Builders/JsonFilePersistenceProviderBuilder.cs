using System;
using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders
{
  public class JsonFilePersistenceProviderBuilder<TKey, TValue>
      : IPersistenceProviderBuilder<TKey, TValue>
      where TKey : notnull
  {
    private string? _filePath;
    private JsonSerializerOptions? _serializerOptions;
    private int _maxRetryAttempts = 3;
    private TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);
    private bool _throwOnPersistenceFailure = false;
    private int _batchSize = 1;
    private TimeSpan _batchInterval = TimeSpan.Zero;

    public JsonFilePersistenceProviderBuilder<TKey, TValue> WithFilePath(string path)
    {
      _filePath = path;
      return this;
    }

    public JsonFilePersistenceProviderBuilder<TKey, TValue> WithSerializerOptions(JsonSerializerOptions opts)
    {
      _serializerOptions = opts;
      return this;
    }

    public JsonFilePersistenceProviderBuilder<TKey, TValue> WithRetry(int maxAttempts, TimeSpan delay)
    {
      _maxRetryAttempts = maxAttempts;
      _retryDelay = delay;
      return this;
    }

    public JsonFilePersistenceProviderBuilder<TKey, TValue> ThrowOnFailure(bool yes = true)
    {
      _throwOnPersistenceFailure = yes;
      return this;
    }

    public JsonFilePersistenceProviderBuilder<TKey, TValue> WithBatch(int batchSize, TimeSpan batchInterval)
    {
      _batchSize = batchSize;
      _batchInterval = batchInterval;
      return this;
    }

    private bool _built = false;

    public IPersistenceProvider<TKey, TValue> Build()
    {
      if (_built)
        throw new InvalidOperationException("This builder instance has already been used to build a provider. Create a new builder for each provider.");

      if (string.IsNullOrWhiteSpace(_filePath))
        throw new InvalidOperationException("FilePath must be set for JsonFilePersistenceProvider.");

      // Validate file path is not just whitespace and is a valid path
      try
      {
        var fullPath = Path.GetFullPath(_filePath!);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("FilePath is not a valid path for JsonFilePersistenceProvider.", ex);
      }

      var options = new JsonFilePersistenceOptions
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
      return new JsonFilePersistenceProvider<TKey, TValue>(options);
    }
  }
}
