namespace PinkRoosterAi.Persistify.Options;

public interface IDatabasePersistenceOptions
{
    string ConnectionString { get; init; }
   
    int MaxRetryAttempts { get; init; }
    TimeSpan RetryDelay { get; init; }
    bool ThrowOnPersistenceFailure { get; init; }
    int BatchSize { get; init; }
    TimeSpan BatchInterval { get; init; }
}