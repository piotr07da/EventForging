using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

internal static class ActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new(EventForgingDiagnosticsInfo.Name, EventForgingDiagnosticsInfo.Version);
}
