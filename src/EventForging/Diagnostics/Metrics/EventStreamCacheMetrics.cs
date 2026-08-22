using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EventForging.Diagnostics.Metrics;

internal static class EventStreamCacheMetrics
{
    public const string LookupMetricName = "eventforging.event_stream_cache.lookup";
    public const string EventsServedMetricName = "eventforging.event_stream_cache.events_served";

    private static readonly Counter<long> LookupCounter = MeterProvider.Meter.CreateCounter<long>(
        LookupMetricName,
        "{lookup}",
        "Event stream cache lookup attempts.");

    private static readonly Counter<long> EventsServedCounter = MeterProvider.Meter.CreateCounter<long>(
        EventsServedMetricName,
        "{event}",
        "Events served from the event stream cache during aggregate rehydration.");

    public static void RecordLookupHit(Type aggregateType)
    {
        RecordLookup(aggregateType, "hit");
    }

    public static void RecordEventsServed(Type aggregateType, long eventCount)
    {
        var tags = CreateAggregateTypeTags(aggregateType);
        EventsServedCounter.Add(eventCount, tags);
    }

    public static void RecordLookupMiss(Type aggregateType)
    {
        RecordLookup(aggregateType, "miss");
    }

    private static void RecordLookup(Type aggregateType, string result)
    {
        var tags = CreateAggregateTypeTags(aggregateType);
        tags.Add(EventStreamCacheMetricTagNames.LookupResult, result);
        LookupCounter.Add(1L, tags);
    }

    private static TagList CreateAggregateTypeTags(Type aggregateType)
    {
        return new TagList
        {
            { EventStreamCacheMetricTagNames.AggregateType, aggregateType.Name },
        };
    }
}
