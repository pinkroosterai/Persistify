namespace PinkRoosterAi.Persistify.Abstractions;

public interface IPersistenceMetadataProvider
{
    /// <summary>
    ///     Returns a map of all keys → their persisted UpdatedAt.
    /// </summary>
    Task<Dictionary<string, DateTime>> LoadLastUpdatedAsync(string dictionaryName,CancellationToken ct = default);
}