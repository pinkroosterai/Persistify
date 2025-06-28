using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders;

public class DatabasePersistenceProviderBuilder<TValue>
    : BasePersistenceProviderBuilder<TValue, DatabasePersistenceOptions, DatabasePersistenceProviderBuilder<TValue>>
{
    private string? _connectionString;

    public DatabasePersistenceProviderBuilder<TValue> WithConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    protected override void ValidateSpecificOptions()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionString must be set for DatabasePersistenceProvider.");
        }
    }

    protected override IPersistenceProvider CreateNonGenericProvider(DatabasePersistenceOptions options)
    {
        return new DatabasePersistenceProvider(options);
    }

    protected override void PopulateSpecificOptions(DatabasePersistenceOptions options)
    {
        options.ConnectionString = _connectionString!;
    }
}