namespace EventForging.Caching.Memory.Metadata;

public static class EventForgingMemoryEventStreamCacheInfo
{
    public static readonly string Name = "EventForging.Caching.Memory";
    public static readonly string Version = typeof(MemoryEventStreamCache).Assembly.GetName().Version?.ToString() ?? string.Empty;
}
