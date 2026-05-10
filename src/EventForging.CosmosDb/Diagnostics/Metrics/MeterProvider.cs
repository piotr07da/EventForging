using System.Diagnostics.Metrics;

namespace EventForging.CosmosDb.Diagnostics.Metrics;

internal static class MeterProvider
{
    public static readonly Meter Meter = new(EventForgingCosmosDbDiagnosticsInfo.MeterName, EventForgingCosmosDbDiagnosticsInfo.Version);
}
