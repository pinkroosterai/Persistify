using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Options;

public class JsonFilePersistenceOptions : IPersistenceOptions
{
    public string FilePath { get; set; } = string.Empty;
    public JsonSerializerOptions? SerializerOptions { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool ThrowOnPersistenceFailure { get; set; } = false;

    // Default to 1 so old behavior stays the same until you opt in
    public int BatchSize { get; set; } = 1;
    public TimeSpan BatchInterval { get; set; } = TimeSpan.Zero;
}