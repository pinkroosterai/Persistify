using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify;

public class CachingPersistentDictionary<TKey, TValue> : PersistentDictionary<TKey, TValue>, IDisposable
{
    private readonly TimeSpan _ttl;
    private readonly object _cacheLock = new();
    private readonly Dictionary<TKey, DateTime> _lastReadAt = new();
    private readonly Dictionary<TKey, DateTime> _lastUpdatedAt = new();

    public CachingPersistentDictionary(IPersistenceProvider<TKey, TValue> persistenceProvider, TimeSpan cachedTimeBeforeEviction)
        : base(persistenceProvider)
    {
        _ttl = cachedTimeBeforeEviction;
    }

    public CachingPersistentDictionary(IPersistenceProvider<TKey, TValue> persistenceProvider, TimeSpan cachedTimeBeforeEviction, ILogger<PersistentDictionary<TKey, TValue>>? logger)
        : base(persistenceProvider, logger)
    {
        _ttl = cachedTimeBeforeEviction;
    }

    public new async Task InitializeAsync(CancellationToken ct = default)
    {
        await base.InitializeAsync(ct);
        var now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            foreach (var key in Keys)
                _lastReadAt[key] = now;
        }

        if (base.PersistenceProvider is IPersistenceMetadataProvider<TKey> metaProvider)
        {
            var updatedDict = await metaProvider.LoadLastUpdatedAsync(ct);
            lock (_cacheLock)
            {
                foreach (var kvp in updatedDict)
                {
                    _lastUpdatedAt[kvp.Key] = kvp.Value;
                }
            }
        }
        EvictExpiredEntries();
    }

    protected override void OnAccess(TKey key)
    {
        lock (_cacheLock)
        {
            _lastReadAt[key] = DateTime.UtcNow;
        }
        EvictExpiredEntries();
    }

    protected override void OnMutation(TKey key)
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
        var now = DateTime.UtcNow;
        var toRemove = new List<TKey>();
        lock (_cacheLock)
        {
            foreach (var key in Keys)
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
            return;

        // _syncRoot is protected in base
        var syncRootField = typeof(PersistentDictionary<TKey, TValue>).GetField("_syncRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        object syncRoot = syncRootField?.GetValue(this) ?? this;

        lock (syncRoot)
        {
            lock (_cacheLock)
            {
                foreach (var key in toRemove)
                {
                    base.Remove(key);
                    _lastReadAt.Remove(key);
                    _lastUpdatedAt.Remove(key);
                }
            }
        }
        foreach (var key in toRemove)
        {
            _ = RemoveAndSaveAsync(key); // fire-and-forget
        }
    }

    public new virtual async Task<bool> RemoveAndSaveAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await base.RemoveAndSaveAsync(key, cancellationToken);
    }
}
