using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify;

/// <summary>
/// Factory helper for creating dictionary instances from persistence providers.
/// Centralizes the creation logic to eliminate duplication across providers.
/// </summary>
internal static class ProviderFactory
{
    public static PersistentDictionary<TValue> CreateDictionary<TValue>(
        IPersistenceProvider provider, 
        string dictionaryName, 
        ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        var adapter = new PersistenceProviderAdapter<TValue>(provider);
        return logger is null 
            ? new PersistentDictionary<TValue>(adapter, dictionaryName)
            : new PersistentDictionary<TValue>(adapter, dictionaryName, logger);
    }

    public static CachingPersistentDictionary<TValue> CreateCachingDictionary<TValue>(
        IPersistenceProvider provider, 
        string dictionaryName, 
        TimeSpan ttl, 
        ILogger<PersistentDictionary<TValue>>? logger = null)
    {
        var adapter = new PersistenceProviderAdapter<TValue>(provider);
        return logger is null 
            ? new CachingPersistentDictionary<TValue>(adapter, dictionaryName, ttl)
            : new CachingPersistentDictionary<TValue>(adapter, dictionaryName, ttl, logger);
    }
}