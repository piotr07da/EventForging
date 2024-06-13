using EventForging.InMemory.Metadata;

namespace EventForging.InMemory.Diagnostics;

public static class EventForgingInMemoryDiagnosticsInfo
{
    public static readonly string Name = EventForgingInMemoryInfo.Name;
    public static readonly string Version = EventForgingInMemoryInfo.Version;

    public static readonly string LoggerCategoryName = Name;
    public static readonly string TracingSourceName = Name;
    public static readonly string MeterName = Name;
}
