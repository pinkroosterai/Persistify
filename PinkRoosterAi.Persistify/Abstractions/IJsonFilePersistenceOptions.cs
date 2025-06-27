using System.Text.Json;

namespace PinkRoosterAi.Persistify.Options;

public interface IJsonFilePersistenceOptions
{
    string FilePath { get; init; }
    JsonSerializerOptions? SerializerOptions { get; init; }
    int MaxRetryAttempts { get; init; }
    TimeSpan RetryDelay { get; init; }
    bool ThrowOnPersistenceFailure { get; init; }
    int BatchSize { get; init; }
    TimeSpan BatchInterval { get; init; }
}