using System.Diagnostics;

namespace EventForging.InMemory.Diagnostics.Tracing;

internal static class ActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new(EventForgingInMemoryDiagnosticsInfo.TracingSourceName, EventForgingInMemoryDiagnosticsInfo.Version);
}
