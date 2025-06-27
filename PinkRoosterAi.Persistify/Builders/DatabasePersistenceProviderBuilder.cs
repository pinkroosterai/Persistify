using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders;

public class DatabasePersistenceProviderBuilder<TValue>
    : IPersistenceProviderBuilder<TValue>, IPersistenceProviderBuilder
{
    private TimeSpan _batchInterval = TimeSpan.Zero;
    private int _batchSize = 1;

    private bool _built;
    private string? _connectionString;

    private int _maxRetryAttempts = 3;
    private TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);

    private bool _throwOnPersistenceFailure;


    public IPersistenceProvider<TValue> Build()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder instance has already been used to build a provider. Create a new builder for each provider.");
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionString must be set for DatabasePersistenceProvider.");
        }


        DatabasePersistenceOptions options = new DatabasePersistenceOptions
        {
            ConnectionString = _connectionString!,
            MaxRetryAttempts = _maxRetryAttempts,
            RetryDelay = _retryDelay,
            ThrowOnPersistenceFailure = _throwOnPersistenceFailure,
            BatchSize = _batchSize,
            BatchInterval = _batchInterval
        };
        _built = true;
        var nonGenericProvider = new DatabasePersistenceProvider(options);
        return new PersistenceProviderAdapter<TValue>(nonGenericProvider);
    }

    IPersistenceProvider IPersistenceProviderBuilder.Build()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder instance has already been used to build a provider. Create a new builder for each provider.");
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionString must be set for DatabasePersistenceProvider.");
        }

        DatabasePersistenceOptions options = new DatabasePersistenceOptions
        {
            ConnectionString = _connectionString!,
            MaxRetryAttempts = _maxRetryAttempts,
            RetryDelay = _retryDelay,
            ThrowOnPersistenceFailure = _throwOnPersistenceFailure,
            BatchSize = _batchSize,
            BatchInterval = _batchInterval
        };
        _built = true;
        return new DatabasePersistenceProvider(options);
    }

    public DatabasePersistenceProviderBuilder<TValue> WithConnectionString(string cs)
    {
        _connectionString = cs;
        return this;
    }



    public DatabasePersistenceProviderBuilder<TValue> WithRetry(int maxAttempts, TimeSpan delay)
    {
        _maxRetryAttempts = maxAttempts;
        _retryDelay = delay;
        return this;
    }

    public DatabasePersistenceProviderBuilder<TValue> ThrowOnFailure(bool yes = true)
    {
        _throwOnPersistenceFailure = yes;
        return this;
    }

    public DatabasePersistenceProviderBuilder<TValue> WithBatch(int batchSize, TimeSpan batchInterval)
    {
        _batchSize = batchSize;
        _batchInterval = batchInterval;
        return this;
    }
}