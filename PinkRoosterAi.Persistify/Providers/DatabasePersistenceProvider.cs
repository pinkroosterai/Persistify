using System.Data;
using System.Text.Json;
using ServiceStack.OrmLite;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;

namespace PinkRoosterAi.Persistify.Providers;

/// <summary>
///     Provides database persistence for PersistentDictionary using ServiceStack.OrmLite.
/// </summary>
/// <typeparam name="TKey">The type of dictionary keys.</typeparam>
/// <typeparam name="TValue">The type of dictionary values.</typeparam>
public class DatabasePersistenceProvider<TKey, TValue> : IPersistenceProvider<TKey, TValue>, IPersistenceMetadataProvider<TKey>
    where TKey : notnull
{
    private readonly OrmLiteConnectionFactory _dbFactory;
    private readonly ILogger<DatabasePersistenceProvider<TKey, TValue>>? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabasePersistenceProvider{TKey, TValue}" /> class.
    /// </summary>
    /// <param name="options">The database persistence options.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DatabasePersistenceProvider(DatabasePersistenceOptions options, ILogger<DatabasePersistenceProvider<TKey, TValue>>? logger = null)
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

    /// <inheritdoc />
    public async Task<Dictionary<TKey, TValue>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<TKey, TValue>();

        var rows = await db.SelectAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            bool keyOk = TryConvertKey(row.Key, out TKey key, out Exception? keyEx);
            bool valueOk = TryConvertValue(row.Value, out TValue value, out Exception? valueEx);

            if (keyOk && valueOk)
            {
                result[key] = value;
            }
            else
            {
                _logger?.LogWarning("Failed to convert row in {Table}: Key='{Key}' Value='{Value}'. KeyError: {KeyError} ValueError: {ValueError}",
                    Options.TableName, row.Key, row.Value,
                    keyEx?.Message, valueEx?.Message);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Dictionary<TKey, TValue> data, CancellationToken cancellationToken = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        // Use async transaction
        using var transaction =  db.BeginTransaction();

        try
        {
            // Collect all keys in the input data
            var inputKeys = new HashSet<string>(data.Keys.Select(k => k?.ToString() ?? throw new InvalidOperationException("Key.ToString() returned null")));

            string Quote(string ident) => "\"" + ident.Replace("\"", "\"\"") + "\"";

            // Use UPSERT (ON CONFLICT) for each row to avoid round-trips
            foreach (var kvp in data)
            {
                string keyString = kvp.Key.ToString() ??
                                   throw new InvalidOperationException("Key.ToString() returned null");
                string valueString = SerializeValue(kvp.Value);

                string upsertSql = $@"
INSERT INTO {Quote(Options.TableName)} ({Quote(Options.KeyColumnName)}, {Quote(Options.ValueColumnName)}, CreatedAt, UpdatedAt)
VALUES (@key, @value, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT({Quote(Options.KeyColumnName)}) DO UPDATE SET
    {Quote(Options.ValueColumnName)} = excluded.{Quote(Options.ValueColumnName)},
    UpdatedAt = CURRENT_TIMESTAMP;";

                await db.ExecuteSqlAsync(upsertSql, new { key = keyString, value = valueString }, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Delete keys not in dictionary
            var existingKeys = await db
                .ColumnAsync<string>($"SELECT {Quote(Options.KeyColumnName)} FROM {Quote(Options.TableName)}", cancellationToken, token: cancellationToken)
                .ConfigureAwait(false);

            foreach (string keyToDelete in existingKeys)
            {
                if (!inputKeys.Contains(keyToDelete))
                {
                    string deleteSql = $@"DELETE FROM {Quote(Options.TableName)} WHERE {Quote(Options.KeyColumnName)} = @key";
                    await db.ExecuteSqlAsync(deleteSql, new { key = keyToDelete }, cancellationToken).ConfigureAwait(false);
                }
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
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Ensures the persistence table exists, creating it if necessary.
    ///     Uses "CREATE TABLE IF NOT EXISTS" for atomicity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
    {
        using IDbConnection? db = await _dbFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        // Use CREATE TABLE IF NOT EXISTS for atomic table creation
        string Quote(string ident) => "\"" + ident.Replace("\"", "\"\"") + "\"";
        string createTableSql = $@"
CREATE TABLE IF NOT EXISTS {Quote(Options.TableName)} (
    {Quote(Options.KeyColumnName)} TEXT PRIMARY KEY,
    {Quote(Options.ValueColumnName)} TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);";
        await db.ExecuteNonQueryAsync(createTableSql, token: cancellationToken).ConfigureAwait(false);
    }

    private bool TryConvertKey(string keyString, out TKey key, out Exception? error)
    {
        key = default!;
        error = null;
        try
        {
            if (typeof(TKey) == typeof(string))
            {
                key = (TKey)(object)keyString!;
                return true;
            }

            if (typeof(TKey).IsEnum)
            {
                key = (TKey)Enum.Parse(typeof(TKey), keyString!);
                return true;
            }

            key = (TKey)Convert.ChangeType(keyString, typeof(TKey));
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
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

    private class TableRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public async Task<Dictionary<TKey, DateTime>> LoadLastUpdatedAsync(CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct).ConfigureAwait(false);

        using IDbConnection? db = await _dbFactory.OpenAsync(ct).ConfigureAwait(false);
        var dict = new Dictionary<TKey, DateTime>();

        string sql = $"SELECT {Options.KeyColumnName}, UpdatedAt FROM {Options.TableName}";
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;

        using IDataReader reader =  cmd.ExecuteReader();
        while ( reader.Read())
        {
            string keyString = reader.GetString(0);
            DateTime updatedAt = reader.GetDateTime(1);

            if (TryConvertKey(keyString, out TKey key, out _))
            {
                dict[key] = updatedAt;
            }
        }

        return dict;
    }
}
