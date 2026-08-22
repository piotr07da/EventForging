namespace EventForging.Caching.Memory;

internal sealed record MemoryEventStreamCacheEntrySnapshot(object[] Events, AggregateVersion Version);
