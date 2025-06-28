using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using ServiceStack.OrmLite;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
/// Database persistence provider that handles different value types at runtime.
/// </summary>
public class DatabasePersistenceProvider : IPersistenceProvider, IPersistenceProvider<object>, IPersistenceMetadataProvider
{
    private static readonly Regex ValidTableNameRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private readonly OrmLiteConnectionFactory _dbFactory;
    private readonly ILogger<DatabasePersistenceProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabasePersistenceProvider" /> class.
    /// </summary>
    /// <param name="options">The database persistence options.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DatabasePersistenceProvider(DatabasePersistenceOptions options,
        ILogger<DatabasePersistenceProvider>? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(Options.ConnectionString))
        {
            throw new ArgumentException("ConnectionString must be specified in DatabasePersistenceOptions.",
                nameof(options));
        }

        _dbFactory = new OrmLiteConnectionFactory(Options.ConnectionString, SqliteDialect.Provider);
        _logger = logger;
    }

    public DatabasePersistenceOptions Options { get; }
    
    IPersistenceOptions IPersistenceProvider.Options => Options;
    IPersistenceOptions IPersistenceProvider<object>.Options => Options;

    public async Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(string dictionaryName, CancellationToken ct = default)
    {
        ValidateDictionaryName(dictionaryName);
        await EnsureTableExistsAsync(dictionaryName, ct).ConfigureAwait(false);

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

    public async Task<Dictionary<string, object>> LoadAsync(string dictionaryName, Type valueType, CancellationToken cancellationToken = default)
    {
        ValidateDictionaryName(dictionaryName);
        await EnsureTableExistsAsync(dictionaryName, cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, object>();

        string sql = $"SELECT {Options.KeyColumnName}, {Options.ValueColumnName} FROM {dictionaryName}";
        var rows = await db.SelectAsync<(string Key, string Value)>(sql, cancellationToken).ConfigureAwait(false);

        foreach ((string Key, string Value) row in rows)
        {
            bool valueOk = TryConvertValue(row.Value, valueType, out object value, out Exception? valueEx);

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

    public async Task SaveAsync(string dictionaryName, Type valueType, Dictionary<string, object> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        
        ValidateDictionaryName(dictionaryName);
        await EnsureTableExistsAsync(dictionaryName, cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        using IDbTransaction transaction = db.BeginTransaction();

        try
        {
            var inputKeys = new HashSet<string>(data.Keys);

            string Quote(string ident)
            {
                return "\"" + ident.Replace("\"", "\"\"") + "\"";
            }

            // Use UPSERT for each row
            foreach (var kvp in data)
            {
                string keyString = kvp.Key;
                string valueString = SerializeValue(kvp.Value, valueType);

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

    public async Task<bool> ExistsAsync(string dictionaryName, CancellationToken cancellationToken = default)
    {
        ValidateDictionaryName(dictionaryName);
        await EnsureTableExistsAsync(dictionaryName, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public PersistentDictionary<TValue> CreateDictionary<TValue>(string dictionaryName, ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        return ProviderFactory.CreateDictionary(this, dictionaryName, logger);
    }

    public CachingPersistentDictionary<TValue> CreateCachingDictionary<TValue>(string dictionaryName, TimeSpan ttl, 
                      ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        return ProviderFactory.CreateCachingDictionary(this, dictionaryName, ttl, logger);
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

    private static void ValidateDictionaryName(string dictionaryName)
    {
        if (string.IsNullOrWhiteSpace(dictionaryName))
        {
            throw new ArgumentException("Dictionary name cannot be null or whitespace.", nameof(dictionaryName));
        }
        
        if (!ValidTableNameRegex.IsMatch(dictionaryName))
        {
            throw new ArgumentException(
                $"Invalid dictionary name '{dictionaryName}'. Must be a valid SQL identifier: " +
                "start with letter or underscore, contain only letters, numbers, and underscores.", 
                nameof(dictionaryName));
        }
    }
    
    private async Task EnsureTableExistsAsync(string dictionaryName, CancellationToken cancellationToken = default)
    {
        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);

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

    private bool TryConvertValue(string valueString, Type valueType, out object value, out Exception? error)
    {
        value = null!;
        error = null;
        try
        {
            if (valueType == typeof(string))
            {
                value = valueString!;
                return true;
            }

            if (valueType.IsEnum)
            {
                value = Enum.Parse(valueType, valueString!);
                return true;
            }

            value = JsonSerializer.Deserialize(valueString, valueType)!;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private string SerializeValue(object value, Type valueType)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        if (valueType.IsEnum)
        {
            return value.ToString() ?? string.Empty;
        }

        return JsonSerializer.Serialize(value);
    }
}