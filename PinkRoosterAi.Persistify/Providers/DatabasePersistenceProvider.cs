using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using ServiceStack.OrmLite;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
///     Provides database persistence for PersistentDictionary using ServiceStack.OrmLite.
/// </summary>
/// <typeparam name="TValue">The type of dictionary values.</typeparam>
public class DatabasePersistenceProvider<TValue> : IPersistenceProvider<TValue>,
    IPersistenceMetadataProvider
{
    private readonly OrmLiteConnectionFactory _dbFactory;
    private readonly ILogger<DatabasePersistenceProvider<TValue>>? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabasePersistenceProvider{TValue}" /> class.
    /// </summary>
    /// <param name="options">The database persistence options.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DatabasePersistenceProvider(DatabasePersistenceOptions options,
        ILogger<DatabasePersistenceProvider<TValue>>? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(Options.ConnectionString))
        {
            throw new ArgumentException("ConnectionString must be specified in DatabasePersistenceOptions.",
                nameof(options));
        }

        // TODO: Allow dialect to be specified in options or inferred from connection string.
        _dbFactory = new OrmLiteConnectionFactory(Options.ConnectionString, SqliteDialect.Provider);
        _logger = logger;
        // Note: SqliteDialect.Provider is default; user can change if needed by extending this class or options.
    }

    public DatabasePersistenceOptions Options { get; }
    
    IPersistenceOptions IPersistenceProvider<TValue>.Options => Options;

    public async Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(string dictionaryName,CancellationToken ct = default)
    {
        await EnsureTableExistsAsync( dictionaryName,ct).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(ct).ConfigureAwait(false);
        var dict = new Dictionary<string, DateTime>();

        string sql = $"SELECT {Options.KeyColumnName}, UpdatedAt FROM {dictionaryName}";
        IDbCommand cmd = db.CreateCommand();
        cmd.CommandText = sql;

        using IDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string keyString = reader.GetString(0);
            DateTime updatedAt = reader.GetDateTime(1);

            dict[keyString] = updatedAt;
        }

        return dict;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, TValue>> LoadAsync(string dictionaryName,CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync( dictionaryName,cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, TValue>();

        // Use the actual table name, not TableRow type
        string sql = $"SELECT {Options.KeyColumnName}, {Options.ValueColumnName} FROM {dictionaryName}";
        var rows = await db.SelectAsync<(string Key, string Value)>(sql, cancellationToken).ConfigureAwait(false);

        foreach ((string Key, string Value) row in rows)
        {
            bool valueOk = TryConvertValue(row.Value, out TValue value, out Exception? valueEx);

            if (valueOk)
            {
                result[row.Key] = value;
            }
            else
            {
                _logger?.LogWarning(
                    "Failed to convert row in {Table}: Key='{Key}' Value='{Value}'. ValueError: {ValueError}",
                    dictionaryName, row.Key, row.Value, valueEx?.Message);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveAsync(string dictionaryName,Dictionary<string, TValue> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await EnsureTableExistsAsync( dictionaryName,cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        // Use async transaction
        using IDbTransaction transaction = db.BeginTransaction();

        try
        {
            // Collect all keys in the input data
            var inputKeys = new HashSet<string>(data.Keys);

            string Quote(string ident)
            {
                return "\"" + ident.Replace("\"", "\"\"") + "\"";
            }

            // Use UPSERT (ON CONFLICT) for each row to avoid round-trips
            foreach (var kvp in data)
            {
                string keyString = kvp.Key;
                string valueString = SerializeValue(kvp.Value);

                string upsertSql = $@"
INSERT INTO {Quote(dictionaryName)} ({Quote(Options.KeyColumnName)}, {Quote(Options.ValueColumnName)}, CreatedAt, UpdatedAt)
VALUES (@key, @value, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT({Quote(Options.KeyColumnName)}) DO UPDATE SET
    {Quote(Options.ValueColumnName)} = excluded.{Quote(Options.ValueColumnName)},
    UpdatedAt = CURRENT_TIMESTAMP;";

                await db.ExecuteSqlAsync(upsertSql, new { key = keyString, value = valueString }, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Delete keys not in dictionary
            var existingKeys = await db
                .ColumnAsync<string>($"SELECT {Quote(Options.KeyColumnName)} FROM {Quote(dictionaryName)}",
                    cancellationToken, cancellationToken)
                .ConfigureAwait(false);

            foreach (string keyToDelete in existingKeys)
                if (!inputKeys.Contains(keyToDelete))
                {
                    string deleteSql =
                        $@"DELETE FROM {Quote(dictionaryName)} WHERE {Quote(Options.KeyColumnName)} = @key";
                    await db.ExecuteSqlAsync(deleteSql, new { key = keyToDelete }, cancellationToken)
                        .ConfigureAwait(false);
                }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string dictionaryName,CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync( dictionaryName,cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Ensures the persistence table exists, creating it if necessary.
    ///     Uses "CREATE TABLE IF NOT EXISTS" for atomicity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task EnsureTableExistsAsync(string dictionaryName,CancellationToken cancellationToken = default)
    {
        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Use CREATE TABLE IF NOT EXISTS for atomic table creation
        string Quote(string ident)
        {
            return "\"" + ident.Replace("\"", "\"\"") + "\"";
        }

        string createTableSql = $@"
CREATE TABLE IF NOT EXISTS {Quote(dictionaryName)} (
    {Quote(Options.KeyColumnName)} TEXT PRIMARY KEY,
    {Quote(Options.ValueColumnName)} TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);";
        await db.ExecuteNonQueryAsync(createTableSql, cancellationToken).ConfigureAwait(false);
    }


    private bool TryConvertValue(string valueString, out TValue value, out Exception? error)
    {
        value = default!;
        error = null;
        try
        {
            if (typeof(TValue) == typeof(string))
            {
                value = (TValue)(object)valueString!;
                return true;
            }

            if (typeof(TValue).IsEnum)
            {
                value = (TValue)Enum.Parse(typeof(TValue), valueString!);
                return true;
            }

            value = JsonSerializer.Deserialize<TValue>(valueString)!;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private string SerializeValue(TValue value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        if (value.GetType().IsEnum)
        {
            return value.ToString() ?? string.Empty;
        }

        return JsonSerializer.Serialize(value);
    }

    public PersistentDictionary<TValue> CreateDictionary(string dictionaryName,ILogger<PersistentDictionary<TValue>>? logger = null)
        => logger is null ? new PersistentDictionary<TValue>(this, dictionaryName)
                          : new PersistentDictionary<TValue>(this,dictionaryName, logger);

    public CachingPersistentDictionary<TValue> CreateCachingDictionary(string dictionaryName,TimeSpan ttl, 
                          ILogger<PersistentDictionary<TValue>>? logger = null)
        => logger is null ? new CachingPersistentDictionary<TValue>(this,dictionaryName, ttl)
                          : new CachingPersistentDictionary<TValue>(this,dictionaryName, ttl, logger);

    private class TableRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}