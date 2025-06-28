using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Options;

public class DatabasePersistenceOptions : IPersistenceOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string KeyColumnName { get; set; } = "Key";
    public string ValueColumnName { get; set; } = "Value";
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool ThrowOnPersistenceFailure { get; set; } = false;

    // Default to 1 so old behavior stays the same until you opt in
    public int BatchSize { get; set; } = 1;
    public TimeSpan BatchInterval { get; set; } = TimeSpan.Zero;
}