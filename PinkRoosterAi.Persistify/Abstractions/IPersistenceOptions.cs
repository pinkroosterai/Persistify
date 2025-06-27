namespace PinkRoosterAi.Persistify.Abstractions;

public interface IPersistenceOptions
{
    int MaxRetryAttempts { get; }
    TimeSpan RetryDelay { get; }
    bool ThrowOnPersistenceFailure { get; }

    /// <summary>
    /// Number of buffered mutations before automatically flushing.
    /// </summary>
    int BatchSize { get; }

    /// <summary>
    /// Maximum time to wait before flushing even if BatchSize not reached.
    /// </summary>
    TimeSpan BatchInterval { get; }
}
