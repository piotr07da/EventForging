using EventForging.Metadata;

namespace EventForging.Diagnostics;

public static class EventForgingDiagnosticsInfo
{
    public static readonly string Name = EventForgingInfo.Name;
    public static readonly string Version = EventForgingInfo.Version;

    public static readonly string LoggingSourceName = Name;
    public static readonly string TracingSourceName = Name;
    public static readonly string MetricsSourceName = Name;
}
