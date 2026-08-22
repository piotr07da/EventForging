namespace EventForging.Caching.Memory;

internal readonly record struct MemoryEventStreamCacheEntryRemoval(Type AggregateType, int EventCount, string Reason);
