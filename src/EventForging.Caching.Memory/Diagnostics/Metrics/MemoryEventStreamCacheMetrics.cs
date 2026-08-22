using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EventForging.Caching.Memory.Diagnostics.Metrics;

internal static class MemoryEventStreamCacheMetrics
{
    public const string CachedStreamsMetricName = "eventforging.event_stream_cache.cached_streams";
    public const string CachedEventsMetricName = "eventforging.event_stream_cache.cached_events";
    public const string EntryRemovalMetricName = "eventforging.event_stream_cache.entry_removal";

    private static readonly UpDownCounter<long> CachedStreamsCounter = MeterProvider.Meter.CreateUpDownCounter<long>(
        CachedStreamsMetricName,
        "{stream}",
        "Current number of event streams stored in the memory event stream cache.");

    private static readonly UpDownCounter<long> CachedEventsCounter = MeterProvider.Meter.CreateUpDownCounter<long>(
        CachedEventsMetricName,
        "{event}",
        "Current number of events stored in the memory event stream cache.");

    private static readonly Counter<long> EntryRemovalCounter = MeterProvider.Meter.CreateCounter<long>(
        EntryRemovalMetricName,
        "{removal}",
        "Entries removed from the memory event stream cache.");

    public static void RecordStreamAdded(Type aggregateType, int eventCount)
    {
        var tags = CreateAggregateTypeTags(aggregateType);
        CachedStreamsCounter.Add(1L, tags);
        CachedEventsCounter.Add(eventCount, tags);
    }

    public static void RecordStreamEventCountChanged(Type aggregateType, int eventCountChange)
    {
        var tags = CreateAggregateTypeTags(aggregateType);
        CachedEventsCounter.Add(eventCountChange, tags);
    }

    public static void RecordEntryRemoval(Type aggregateType, int eventCount, string reason)
    {
        var aggregateTypeTags = CreateAggregateTypeTags(aggregateType);
        CachedStreamsCounter.Add(-1L, aggregateTypeTags);
        CachedEventsCounter.Add(-eventCount, aggregateTypeTags);

        var entryRemovalTags = aggregateTypeTags;
        entryRemovalTags.Add(MemoryEventStreamCacheMetricTagNames.EntryRemovalReason, reason);
        EntryRemovalCounter.Add(1L, entryRemovalTags);
    }

    private static TagList CreateAggregateTypeTags(Type aggregateType)
    {
        return new TagList
        {
            { MemoryEventStreamCacheMetricTagNames.AggregateType, aggregateType.Name },
        };
    }
}
