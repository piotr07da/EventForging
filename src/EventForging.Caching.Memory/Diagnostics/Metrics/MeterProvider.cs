using System.Diagnostics.Metrics;

namespace EventForging.Caching.Memory.Diagnostics.Metrics;

internal static class MeterProvider
{
    public static readonly Meter Meter = new(EventForgingMemoryEventStreamCacheDiagnosticsInfo.MeterName, EventForgingMemoryEventStreamCacheDiagnosticsInfo.Version);
}
