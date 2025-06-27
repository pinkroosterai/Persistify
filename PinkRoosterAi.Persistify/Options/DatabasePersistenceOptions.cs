using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Options;

public class DatabasePersistenceOptions : IPersistenceOptions
{
    public required string ConnectionString { get; init; }
    public required string? TableName { get; init; }
    public string KeyColumnName { get; init; } = "Key";
    public string ValueColumnName { get; init; } = "Value";
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public bool ThrowOnPersistenceFailure { get; init; } = false;

    // Default to 1 so old behavior stays the same until you opt in
    public int BatchSize { get; init; } = 1;
    public TimeSpan BatchInterval { get; init; } = TimeSpan.Zero;
}
