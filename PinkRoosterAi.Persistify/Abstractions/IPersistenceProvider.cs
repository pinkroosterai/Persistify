using Microsoft.Extensions.Logging;

namespace PinkRoosterAi.Persistify.Abstractions;

public interface IPersistenceProvider<TValue>
{
    IPersistenceOptions Options { get; }

    Task<Dictionary<string, TValue>> LoadAsync(string dictionaryName,CancellationToken cancellationToken = default);
    Task SaveAsync(string dictionaryName,Dictionary<string, TValue> data, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string dictionaryName,CancellationToken cancellationToken = default);

    PersistentDictionary<TValue> CreateDictionary(string dictionaryName,ILogger<PersistentDictionary<TValue>>? logger = null);
    CachingPersistentDictionary<TValue> CreateCachingDictionary(string dictionaryName,TimeSpan ttl, ILogger<PersistentDictionary<TValue>>? logger = null);
}