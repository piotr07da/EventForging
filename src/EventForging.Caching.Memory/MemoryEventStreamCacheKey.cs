namespace EventForging.Caching.Memory;

internal readonly record struct MemoryEventStreamCacheKey(Type AggregateType, string AggregateId);
