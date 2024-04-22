using System.Diagnostics;
using EventForging.Metadata;

namespace EventForging.Diagnostics.Tracing;

public static class ActivitySourceProvider
{
    public static readonly ActivitySource ActivitySource = new(EventForgingDiagnosticsInfo.Name, EventForgingInfo.Version);
}
