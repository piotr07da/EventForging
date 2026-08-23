using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EventForging.Diagnostics.Metrics;

internal static class EventStreamReadMetrics
{
    public const string CacheLookupsMetricName = "eventforging.event_stream.read.cache.lookups";
    public const string EventsServedMetricName = "eventforging.event_stream.read.events_served";

    private const string CacheSource = "cache";
    private const string DatabaseSource = "database";
    private const string HitResult = "hit";
    private const string MissResult = "miss";

    private static readonly Counter<long> CacheLookupsCounter = MeterProvider.Meter.CreateCounter<long>(
        CacheLookupsMetricName,
        "{lookup}",
        "Event stream cache lookup attempts during aggregate rehydration.");

    private static readonly Counter<long> EventsServedCounter = MeterProvider.Meter.CreateCounter<long>(
        EventsServedMetricName,
        "{event}",
        "Events served during successful event stream reads.");

    public static void RecordCacheLookupHit(Type aggregateType)
    {
        RecordCacheLookup(aggregateType, HitResult);
    }

    public static void RecordCacheLookupMiss(Type aggregateType)
    {
        RecordCacheLookup(aggregateType, MissResult);
    }

    public static void RecordEventsServedFromCache(Type aggregateType, long eventCount)
    {
        RecordEventsServed(aggregateType, eventCount, CacheSource);
    }

    public static void RecordEventsServedFromDatabase(Type aggregateType, long eventCount)
    {
        RecordEventsServed(aggregateType, eventCount, DatabaseSource);
    }

    private static void RecordCacheLookup(Type aggregateType, string result)
    {
        var tags = CreateAggregateTypeTags(aggregateType);
        tags.Add(EventStreamReadMetricTagNames.CacheLookupResult, result);
        CacheLookupsCounter.Add(1L, tags);
    }

    private static void RecordEventsServed(Type aggregateType, long eventCount, string source)
    {
        if (eventCount == 0L)
        {
            return;
        }

        var tags = CreateAggregateTypeTags(aggregateType);
        tags.Add(EventStreamReadMetricTagNames.Source, source);
        EventsServedCounter.Add(eventCount, tags);
    }

    private static TagList CreateAggregateTypeTags(Type aggregateType)
    {
        return new TagList
        {
            { EventStreamReadMetricTagNames.AggregateType, aggregateType.Name },
        };
    }
}
