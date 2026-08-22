namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheEntry
{
    private MemoryEventStreamCacheEntrySnapshot _snapshot;
    private long _lastUsedAtTicks;
    private int _removed;

    public MemoryEventStreamCacheEntry(
        MemoryEventStreamCacheKey cacheKey,
        MemoryEventStreamCacheEntrySnapshot snapshot,
        long lastUsedAtTicks)
    {
        CacheKey = cacheKey;
        _snapshot = snapshot;
        _lastUsedAtTicks = lastUsedAtTicks;
    }

    public MemoryEventStreamCacheKey CacheKey { get; }
    public object StateChangeSync { get; } = new();
    public MemoryEventStreamCacheEntrySnapshot Snapshot => Volatile.Read(ref _snapshot);
    public long LastUsedAtTicks => Interlocked.Read(ref _lastUsedAtTicks);
    public bool Removed => Volatile.Read(ref _removed) != 0;

    public bool IsExpired(long nowTicks, long slidingExpirationTicks)
    {
        return nowTicks - LastUsedAtTicks >= slidingExpirationTicks;
    }

    public bool TryGetSnapshot(long nowTicks, out MemoryEventStreamCacheEntrySnapshot snapshot)
    {
        if (Removed)
        {
            snapshot = null!;
            return false;
        }

        UpdateLastUsedAt(nowTicks);
        snapshot = Snapshot;
        return !Removed;
    }

    public void Touch(long nowTicks)
    {
        UpdateLastUsedAt(nowTicks);
    }

    public void SetSnapshot(MemoryEventStreamCacheEntrySnapshot snapshot, long lastUsedAtTicks)
    {
        Volatile.Write(ref _snapshot, snapshot);
        UpdateLastUsedAt(lastUsedAtTicks);
    }

    public void MarkRemoved()
    {
        Volatile.Write(ref _removed, 1);
    }

    private void UpdateLastUsedAt(long lastUsedAtTicks)
    {
        var currentLastUsedAtTicks = Interlocked.Read(ref _lastUsedAtTicks);
        while (lastUsedAtTicks > currentLastUsedAtTicks)
        {
            var observedLastUsedAtTicks = Interlocked.CompareExchange(
                ref _lastUsedAtTicks,
                lastUsedAtTicks,
                currentLastUsedAtTicks);
            if (observedLastUsedAtTicks == currentLastUsedAtTicks)
            {
                return;
            }

            currentLastUsedAtTicks = observedLastUsedAtTicks;
        }
    }
}
