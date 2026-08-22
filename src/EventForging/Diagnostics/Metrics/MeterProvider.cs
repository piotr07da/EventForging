using System.Diagnostics.Metrics;

namespace EventForging.Diagnostics.Metrics;

internal static class MeterProvider
{
    public static readonly Meter Meter = new(EventForgingDiagnosticsInfo.MeterName, EventForgingDiagnosticsInfo.Version);
}
