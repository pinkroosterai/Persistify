using System.Reflection;
using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify;

public class CachingPersistentDictionary<TValue> : PersistentDictionary<TValue>, IDisposable
{
    private readonly object _cacheLock = new object();
    private readonly Dictionary<string, DateTime> _lastReadAt = new Dictionary<string, DateTime>();
    private readonly Dictionary<string, DateTime> _lastUpdatedAt = new Dictionary<string, DateTime>();
    private readonly TimeSpan _ttl;

    public CachingPersistentDictionary(IPersistenceProvider<TValue> persistenceProvider, string dictionaryName,
        TimeSpan cachedTimeBeforeEviction)
        : base(persistenceProvider,dictionaryName)
    {
        _ttl = cachedTimeBeforeEviction;
    }

    public CachingPersistentDictionary(IPersistenceProvider<TValue> persistenceProvider,string dictionaryName,
        TimeSpan cachedTimeBeforeEviction,  ILogger<PersistentDictionary<TValue>>? logger)
        : base(persistenceProvider,dictionaryName, logger)
    {
        _ttl = cachedTimeBeforeEviction;
    }

    public new async Task InitializeAsync(CancellationToken ct = default)
    {
        await base.InitializeAsync(ct);
        DateTime now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            foreach (string key in Keys)
                _lastReadAt[key] = now;
        }

        if (PersistenceProvider is IPersistenceMetadataProvider metaProvider)
        {
            var updatedDict = await metaProvider.LoadLastUpdatedAsync(DictionaryName,ct).ConfigureAwait(false);
            lock (_cacheLock)
            {
                foreach (var kvp in updatedDict) _lastUpdatedAt[kvp.Key] = kvp.Value;
            }
        }

        EvictExpiredEntries();
    }

    protected override void OnAccess(string key)
    {
        lock (_cacheLock)
        {
            _lastReadAt[key] = DateTime.UtcNow;
        }

        EvictExpiredEntries();
    }

    protected override void OnMutation(string key)
    {
        EvictExpiredEntries();
        lock (_cacheLock)
        {
            _lastReadAt[key] = DateTime.UtcNow;
            _lastUpdatedAt[key] = DateTime.UtcNow;
        }
    }

    private void EvictExpiredEntries()
    {
        DateTime now = DateTime.UtcNow;
        var toRemove = new List<string>();
        lock (_cacheLock)
        {
            foreach (string key in Keys)
            {
                DateTime readAt = _lastReadAt.GetValueOrDefault(key, now);
                DateTime updatedAt = _lastUpdatedAt.GetValueOrDefault(key, now);
                if (now - readAt > _ttl || now - updatedAt > _ttl)
                {
                    toRemove.Add(key);
                }
            }
        }

        if (!toRemove.Any())
        {
            return;
        }

        // _syncRoot is protected in base
        FieldInfo? syncRootField =
            typeof(PersistentDictionary<TValue>).GetField("_syncRoot",
                BindingFlags.NonPublic | BindingFlags.Instance);
        object syncRoot = syncRootField?.GetValue(this) ?? this;

        lock (syncRoot)
        {
            lock (_cacheLock)
            {
                foreach (string key in toRemove)
                {
                    Remove(key);
                    _lastReadAt.Remove(key);
                    _lastUpdatedAt.Remove(key);
                }
            }
        }

        foreach (string key in toRemove) _ = RemoveAndSaveAsync(key); // fire-and-forget
    }

    public new virtual async Task<bool> RemoveAndSaveAsync(string key, CancellationToken cancellationToken = default)
    {
        return await base.RemoveAndSaveAsync(key, cancellationToken);
    }
}