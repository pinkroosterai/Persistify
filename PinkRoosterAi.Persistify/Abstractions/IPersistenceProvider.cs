using Microsoft.Extensions.Logging;

namespace PinkRoosterAi.Persistify.Abstractions;

/// <summary>
/// Non-generic persistence provider interface that handles persistence operations
/// with runtime type information rather than compile-time generics.
/// </summary>
public interface IPersistenceProvider
{
    IPersistenceOptions Options { get; }

    Task<Dictionary<string, object>> LoadAsync(string dictionaryName, Type valueType, CancellationToken cancellationToken = default);
    Task SaveAsync(string dictionaryName, Type valueType, Dictionary<string, object> data, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string dictionaryName, CancellationToken cancellationToken = default);

    PersistentDictionary<TValue> CreateDictionary<TValue>(string dictionaryName, ILogger<PersistentDictionary<TValue>>? logger = null);
    CachingPersistentDictionary<TValue> CreateCachingDictionary<TValue>(string dictionaryName, TimeSpan ttl, ILogger<PersistentDictionary<TValue>>? logger = null);
}

/// <summary>
/// Legacy generic interface for backward compatibility.
/// </summary>
public interface IPersistenceProvider<TValue>
{
    IPersistenceOptions Options { get; }
    
    Task<Dictionary<string, TValue>> LoadAsync(string dictionaryName, CancellationToken cancellationToken = default);
    Task SaveAsync(string dictionaryName, Dictionary<string, TValue> data, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string dictionaryName, CancellationToken cancellationToken = default);

    PersistentDictionary<TValue> CreateDictionary(string dictionaryName, ILogger<PersistentDictionary<TValue>>? logger = null);
    CachingPersistentDictionary<TValue> CreateCachingDictionary(string dictionaryName, TimeSpan ttl, ILogger<PersistentDictionary<TValue>>? logger = null);
}