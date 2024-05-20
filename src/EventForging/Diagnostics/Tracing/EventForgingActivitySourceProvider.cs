using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

public static class EventForgingActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new(EventForgingDiagnosticsInfo.Name, EventForgingDiagnosticsInfo.Version);
}
