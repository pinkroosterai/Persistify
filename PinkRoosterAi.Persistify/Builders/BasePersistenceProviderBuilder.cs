using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Providers;
using PinkRoosterAi.Persistify.Options;

namespace PinkRoosterAi.Persistify.Builders;

/// <summary>
/// Base class for persistence provider builders, containing shared functionality
/// for retry logic, batching, and build state management.
/// </summary>
/// <typeparam name="TValue">The value type for the persistence provider.</typeparam>
/// <typeparam name="TOptions">The options type for the specific provider.</typeparam>
/// <typeparam name="TBuilder">The concrete builder type for fluent chaining.</typeparam>
public abstract class BasePersistenceProviderBuilder<TValue, TOptions, TBuilder>
    : IPersistenceProviderBuilder<TValue>, IPersistenceProviderBuilder
    where TOptions : class, IPersistenceOptions, new()
    where TBuilder : BasePersistenceProviderBuilder<TValue, TOptions, TBuilder>
{
    protected TimeSpan _batchInterval = TimeSpan.Zero;
    protected int _batchSize = 1;
    protected bool _built;
    protected int _maxRetryAttempts = 3;
    protected TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);
    protected bool _throwOnPersistenceFailure;

    protected abstract void ValidateSpecificOptions();
    protected abstract IPersistenceProvider CreateNonGenericProvider(TOptions options);

    public IPersistenceProvider<TValue> Build()
    {
        ValidateBuildState();
        ValidateSpecificOptions();

        var options = CreateOptions();
        _built = true;
        var nonGenericProvider = CreateNonGenericProvider(options);
        return new PersistenceProviderAdapter<TValue>(nonGenericProvider);
    }

    IPersistenceProvider IPersistenceProviderBuilder.Build()
    {
        ValidateBuildState();
        ValidateSpecificOptions();

        var options = CreateOptions();
        _built = true;
        return CreateNonGenericProvider(options);
    }

    public TBuilder WithRetry(int maxAttempts, TimeSpan delay)
    {
        _maxRetryAttempts = maxAttempts;
        _retryDelay = delay;
        return (TBuilder)this;
    }

    public TBuilder ThrowOnFailure(bool yes = true)
    {
        _throwOnPersistenceFailure = yes;
        return (TBuilder)this;
    }

    public TBuilder WithBatch(int batchSize, TimeSpan batchInterval)
    {
        _batchSize = batchSize;
        _batchInterval = batchInterval;
        return (TBuilder)this;
    }

    private void ValidateBuildState()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder instance has already been used to build a provider. Create a new builder for each provider.");
        }
    }

    private TOptions CreateOptions()
    {
        var options = new TOptions();
        PopulateSpecificOptions(options);
        SetBaseOptions(options);
        return options;
    }

    private void SetBaseOptions(TOptions options)
    {
        // Cast to concrete type to set properties
        if (options is JsonFilePersistenceOptions jsonOptions)
        {
            jsonOptions.MaxRetryAttempts = _maxRetryAttempts;
            jsonOptions.RetryDelay = _retryDelay;
            jsonOptions.ThrowOnPersistenceFailure = _throwOnPersistenceFailure;
            jsonOptions.BatchSize = _batchSize;
            jsonOptions.BatchInterval = _batchInterval;
        }
        else if (options is DatabasePersistenceOptions dbOptions)
        {
            dbOptions.MaxRetryAttempts = _maxRetryAttempts;
            dbOptions.RetryDelay = _retryDelay;
            dbOptions.ThrowOnPersistenceFailure = _throwOnPersistenceFailure;
            dbOptions.BatchSize = _batchSize;
            dbOptions.BatchInterval = _batchInterval;
        }
    }

    protected abstract void PopulateSpecificOptions(TOptions options);
}