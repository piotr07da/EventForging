using System.Collections.Concurrent;
using EventForging.Caching.Memory.Diagnostics.Metrics;

namespace EventForging.Caching.Memory;

// _cacheEntries supports lock-free reads; _entryCollectionSync coordinates only compound structural changes.
// ReSharper disable InconsistentlySynchronizedField
internal sealed class MemoryEventStreamCache
{
    private readonly object _entryCollectionSync = new();
    private readonly ConcurrentDictionary<MemoryEventStreamCacheKey, MemoryEventStreamCacheEntry> _cacheEntries = new();
    private readonly IMemoryEventStreamCacheConfiguration _configuration;
    private long _cachedEventCount;

    public MemoryEventStreamCache(IMemoryEventStreamCacheConfiguration configuration)
    {
        _configuration = configuration;
    }

    internal bool TryGetSnapshot(
        MemoryEventStreamCacheKey cacheKey,
        out MemoryEventStreamCacheEntrySnapshot snapshot)
    {
        while (_cacheEntries.TryGetValue(cacheKey, out var cacheEntry))
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            if (cacheEntry.IsExpired(nowTicks, _configuration.SlidingExpiration.Ticks))
            {
                var expiration = TryRemoveExpiredEntry(cacheKey, cacheEntry, nowTicks);
                if (expiration is not null)
                {
                    RecordEntryRemoval(expiration.Value);
                    snapshot = null!;
                    return false;
                }

                continue;
            }

            if (cacheEntry.TryGetSnapshot(nowTicks, out snapshot))
            {
                return true;
            }
        }

        snapshot = null!;
        return false;
    }

    internal void Invalidate<TAggregate>(string aggregateId)
    {
        var entryRemoval = TryRemoveEntry(
            new MemoryEventStreamCacheKey(typeof(TAggregate), aggregateId),
            MemoryEventStreamCacheEntryRemovalReasons.Invalidated);
        if (entryRemoval is not null)
        {
            RecordEntryRemoval(entryRemoval.Value);
        }
    }

    internal void CompleteWrite(
        MemoryEventStreamCacheKey cacheKey,
        AggregateVersion firstEventVersion,
        AggregateVersion lastEventVersion,
        object[] events,
        bool streamExceedsTotalEventCapacity)
    {
        if (streamExceedsTotalEventCapacity)
        {
            var entryRemoval = TryRemoveEntry(
                cacheKey,
                MemoryEventStreamCacheEntryRemovalReasons.EventCountLimit);
            if (entryRemoval is not null)
            {
                RecordEntryRemoval(entryRemoval.Value);
            }

            return;
        }

        StoreCompletedEvents(
            cacheKey,
            firstEventVersion,
            lastEventVersion,
            events);
    }

    private void StoreCompletedEvents(
        MemoryEventStreamCacheKey cacheKey,
        AggregateVersion firstEventVersion,
        AggregateVersion lastEventVersion,
        object[] events)
    {
        while (true)
        {
            if (_cacheEntries.TryGetValue(cacheKey, out var cacheEntry))
            {
                if (!TryUpdateStream(cacheKey, cacheEntry, firstEventVersion, lastEventVersion, events, out var appendedEventCount))
                {
                    continue;
                }

                if (appendedEventCount > 0)
                {
                    MemoryEventStreamCacheMetrics.RecordStreamEventCountChanged(cacheKey.AggregateType, appendedEventCount);
                    TrimToCapacity();
                }

                return;
            }

            if (firstEventVersion.Value != 0L
                || lastEventVersion.Next().Value < _configuration.MinimumEventCount)
            {
                return;
            }

            if (TryAddStream(cacheKey, lastEventVersion, events))
            {
                return;
            }
        }
    }

    private bool TryAddStream(
        MemoryEventStreamCacheKey cacheKey,
        AggregateVersion lastEventVersion,
        object[] events)
    {
        List<MemoryEventStreamCacheEntryRemoval> entryRemovals;
        lock (_entryCollectionSync)
        {
            var cacheEntry = new MemoryEventStreamCacheEntry(
                cacheKey,
                new MemoryEventStreamCacheEntrySnapshot(events, lastEventVersion),
                DateTime.UtcNow.Ticks);
            if (!_cacheEntries.TryAdd(cacheKey, cacheEntry))
            {
                return false;
            }

            Interlocked.Add(ref _cachedEventCount, events.Length);
            entryRemovals = TrimToCapacityUnderCollectionLock();
        }

        MemoryEventStreamCacheMetrics.RecordStreamAdded(cacheKey.AggregateType, events.Length);
        RecordEntryRemovals(entryRemovals);
        return true;
    }

    private bool TryUpdateStream(
        MemoryEventStreamCacheKey cacheKey,
        MemoryEventStreamCacheEntry cacheEntry,
        AggregateVersion firstEventVersion,
        AggregateVersion lastEventVersion,
        object[] events,
        out int appendedEventCount)
    {
        lock (cacheEntry.StateChangeSync)
        {
            if (!IsCurrentEntry(cacheKey, cacheEntry))
            {
                appendedEventCount = 0;
                return false;
            }

            var snapshot = cacheEntry.Snapshot;
            if (snapshot.Version.Value >= lastEventVersion.Value)
            {
                appendedEventCount = 0;
                cacheEntry.Touch(DateTime.UtcNow.Ticks);
                return true;
            }

            if (firstEventVersion.Value > snapshot.Version.Next().Value)
            {
                appendedEventCount = 0;
                return true;
            }

            var overlappingEventCount = (int)Math.Max(0L, snapshot.Version.Next().Value - firstEventVersion.Value);
            appendedEventCount = events.Length - overlappingEventCount;
            var combinedEvents = CombineEvents(snapshot.Events, events, overlappingEventCount);
            cacheEntry.SetSnapshot(
                new MemoryEventStreamCacheEntrySnapshot(combinedEvents, lastEventVersion),
                DateTime.UtcNow.Ticks);
            Interlocked.Add(ref _cachedEventCount, appendedEventCount);
            return true;
        }
    }

    private MemoryEventStreamCacheEntryRemoval? TryRemoveExpiredEntry(
        MemoryEventStreamCacheKey cacheKey,
        MemoryEventStreamCacheEntry cacheEntry,
        long nowTicks)
    {
        lock (_entryCollectionSync)
        {
            lock (cacheEntry.StateChangeSync)
            {
                if (!IsCurrentEntry(cacheKey, cacheEntry)
                    || !cacheEntry.IsExpired(nowTicks, _configuration.SlidingExpiration.Ticks))
                {
                    return null;
                }

                return RemoveEntryUnderLocks(cacheEntry, MemoryEventStreamCacheEntryRemovalReasons.Expiration);
            }
        }
    }

    private MemoryEventStreamCacheEntryRemoval? TryRemoveEntry(
        MemoryEventStreamCacheKey cacheKey,
        string reason)
    {
        lock (_entryCollectionSync)
        {
            if (!_cacheEntries.TryGetValue(cacheKey, out var cacheEntry))
            {
                return null;
            }

            lock (cacheEntry.StateChangeSync)
            {
                return RemoveEntryUnderLocks(cacheEntry, reason);
            }
        }
    }

    private bool IsCurrentEntry(
        MemoryEventStreamCacheKey cacheKey,
        MemoryEventStreamCacheEntry cacheEntry)
    {
        return !cacheEntry.Removed
               && _cacheEntries.TryGetValue(cacheKey, out var currentEntry)
               && ReferenceEquals(cacheEntry, currentEntry);
    }

    private MemoryEventStreamCacheEntryRemoval RemoveEntryUnderLocks(
        MemoryEventStreamCacheEntry cacheEntry,
        string reason)
    {
        cacheEntry.MarkRemoved();
        if (!_cacheEntries.TryRemove(cacheEntry.CacheKey, out var removedEntry)
            || !ReferenceEquals(cacheEntry, removedEntry))
        {
            throw new EventForgingException("Memory event stream cache entry changed while it was being removed.");
        }

        var eventCount = cacheEntry.Snapshot.Events.Length;
        Interlocked.Add(ref _cachedEventCount, -eventCount);
        return new MemoryEventStreamCacheEntryRemoval(cacheEntry.CacheKey.AggregateType, eventCount, reason);
    }

    private void TrimToCapacity()
    {
        if (!IsCapacityExceeded())
        {
            return;
        }

        List<MemoryEventStreamCacheEntryRemoval> entryRemovals;
        lock (_entryCollectionSync)
        {
            entryRemovals = TrimToCapacityUnderCollectionLock();
        }

        RecordEntryRemovals(entryRemovals);
    }

    private bool IsCapacityExceeded()
    {
        return _cacheEntries.Count > _configuration.MaximumCachedStreamCount
               || Interlocked.Read(ref _cachedEventCount) > _configuration.MaximumTotalCachedEventCount;
    }

    private List<MemoryEventStreamCacheEntryRemoval> TrimToCapacityUnderCollectionLock()
    {
        var entryRemovals = new List<MemoryEventStreamCacheEntryRemoval>();
        while (IsCapacityExceeded())
        {
            var reason = _cacheEntries.Count > _configuration.MaximumCachedStreamCount
                ? MemoryEventStreamCacheEntryRemovalReasons.StreamCountLimit
                : MemoryEventStreamCacheEntryRemovalReasons.EventCountLimit;
            var leastRecentlyUsedEntry = FindLeastRecentlyUsedEntry();
            if (leastRecentlyUsedEntry is null)
            {
                throw new EventForgingException("Memory event stream cache capacity is exceeded, but the cache contains no removable entries.");
            }

            lock (leastRecentlyUsedEntry.StateChangeSync)
            {
                entryRemovals.Add(RemoveEntryUnderLocks(leastRecentlyUsedEntry, reason));
            }
        }

        return entryRemovals;
    }

    private MemoryEventStreamCacheEntry? FindLeastRecentlyUsedEntry()
    {
        MemoryEventStreamCacheEntry? leastRecentlyUsedEntry = null;
        var leastRecentUseTicks = long.MaxValue;
        foreach (var cacheEntry in _cacheEntries.Values)
        {
            var cacheEntryLastUsedAtTicks = cacheEntry.LastUsedAtTicks;
            if (cacheEntryLastUsedAtTicks < leastRecentUseTicks)
            {
                leastRecentlyUsedEntry = cacheEntry;
                leastRecentUseTicks = cacheEntryLastUsedAtTicks;
            }
        }

        return leastRecentlyUsedEntry;
    }

    private static object[] CombineEvents(object[] cachedEvents, object[] events, int overlappingEventCount)
    {
        var appendedEventCount = events.Length - overlappingEventCount;
        var combinedEvents = new object[cachedEvents.Length + appendedEventCount];
        Array.Copy(cachedEvents, combinedEvents, cachedEvents.Length);
        Array.Copy(events, overlappingEventCount, combinedEvents, cachedEvents.Length, appendedEventCount);
        return combinedEvents;
    }

    private static void RecordEntryRemovals(List<MemoryEventStreamCacheEntryRemoval> entryRemovals)
    {
        for (var entryRemovalIndex = 0; entryRemovalIndex < entryRemovals.Count; ++entryRemovalIndex)
        {
            RecordEntryRemoval(entryRemovals[entryRemovalIndex]);
        }
    }

    private static void RecordEntryRemoval(MemoryEventStreamCacheEntryRemoval entryRemoval)
    {
        MemoryEventStreamCacheMetrics.RecordEntryRemoval(
            entryRemoval.AggregateType,
            entryRemoval.EventCount,
            entryRemoval.Reason);
    }
}
// ReSharper restore InconsistentlySynchronizedField
