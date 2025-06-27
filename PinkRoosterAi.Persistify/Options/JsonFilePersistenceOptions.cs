using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Options;

public class JsonFilePersistenceOptions : IPersistenceOptions
{
    public required string FilePath { get; init; }
    public JsonSerializerOptions? SerializerOptions { get; init; }
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public bool ThrowOnPersistenceFailure { get; init; } = false;

    // Default to 1 so old behavior stays the same until you opt in
    public int BatchSize { get; init; } = 1;
    public TimeSpan BatchInterval { get; init; } = TimeSpan.Zero;
}