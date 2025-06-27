namespace PinkRoosterAi.Persistify.Abstractions;

public interface IPersistenceProvider<TKey, TValue>
{
    Task<Dictionary<TKey, TValue>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Dictionary<TKey, TValue> data, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}