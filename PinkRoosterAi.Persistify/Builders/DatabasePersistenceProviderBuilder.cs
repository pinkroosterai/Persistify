using System;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders
{
  public class DatabasePersistenceProviderBuilder<TKey, TValue>
      : IPersistenceProviderBuilder<TKey, TValue>
      where TKey : notnull
  {
    private string? _connectionString;
    private string _tableName = "PersistentDictionary";
    private string _keyColumnName = "Key";
    private string _valueColumnName = "Value";
    private int _maxRetryAttempts = 3;
    private TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);
    private bool _throwOnPersistenceFailure = false;
    private int _batchSize = 1;
    private TimeSpan _batchInterval = TimeSpan.Zero;

    public DatabasePersistenceProviderBuilder<TKey, TValue> WithConnectionString(string cs)
    {
      _connectionString = cs;
      return this;
    }

    public DatabasePersistenceProviderBuilder<TKey, TValue> WithTableName(string table)
    {
      _tableName = table;
      return this;
    }

    public DatabasePersistenceProviderBuilder<TKey, TValue> WithColumns(string keyColumn, string valueColumn)
    {
      _keyColumnName = keyColumn;
      _valueColumnName = valueColumn;
      return this;
    }

    public DatabasePersistenceProviderBuilder<TKey, TValue> WithRetry(int maxAttempts, TimeSpan delay)
    {
      _maxRetryAttempts = maxAttempts;
      _retryDelay = delay;
      return this;
    }

    public DatabasePersistenceProviderBuilder<TKey, TValue> ThrowOnFailure(bool yes = true)
    {
      _throwOnPersistenceFailure = yes;
      return this;
    }

    public DatabasePersistenceProviderBuilder<TKey, TValue> WithBatch(int batchSize, TimeSpan batchInterval)
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

      if (string.IsNullOrWhiteSpace(_connectionString))
        throw new InvalidOperationException("ConnectionString must be set for DatabasePersistenceProvider.");



      var options = new DatabasePersistenceOptions
      {
        ConnectionString = _connectionString!,
        TableName = _tableName ?? typeof(TValue).Name,
        KeyColumnName = _keyColumnName,
        ValueColumnName = _valueColumnName,
        MaxRetryAttempts = _maxRetryAttempts,
        RetryDelay = _retryDelay,
        ThrowOnPersistenceFailure = _throwOnPersistenceFailure,
        BatchSize = _batchSize,
        BatchInterval = _batchInterval
      };
      _built = true;
      return new DatabasePersistenceProvider<TKey, TValue>(options);
    }
  }
}
