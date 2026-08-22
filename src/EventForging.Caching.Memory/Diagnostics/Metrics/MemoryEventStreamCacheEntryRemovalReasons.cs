namespace EventForging.Caching.Memory.Diagnostics.Metrics;

internal static class MemoryEventStreamCacheEntryRemovalReasons
{
    public const string Expiration = "expiration";
    public const string StreamCountLimit = "stream_count_limit";
    public const string EventCountLimit = "event_count_limit";
    public const string Invalidated = "invalidated";
}
