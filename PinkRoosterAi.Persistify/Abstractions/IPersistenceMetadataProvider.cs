namespace PinkRoosterAi.Persistify.Abstractions;

public interface IPersistenceMetadataProvider<TKey>
{
    /// <summary>
    /// Returns a map of all keys → their persisted UpdatedAt.
    /// </summary>
    Task<Dictionary<TKey, DateTime>> LoadLastUpdatedAsync(CancellationToken ct = default);
}
