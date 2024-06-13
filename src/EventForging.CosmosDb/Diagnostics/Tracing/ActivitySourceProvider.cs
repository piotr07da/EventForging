using System.Diagnostics;

namespace EventForging.CosmosDb.Diagnostics.Tracing;

internal static class ActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new(EventForgingCosmosDbDiagnosticsInfo.Name, EventForgingCosmosDbDiagnosticsInfo.Version);
}
