using EventForging.CosmosDb.Metadata;

namespace EventForging.CosmosDb.Diagnostics;

public static class EventForgingCosmosDbDiagnosticsInfo
{
    public static readonly string Name = EventForgingCosmosDbInfo.Name;
    public static readonly string Version = EventForgingCosmosDbInfo.Version;

    public static readonly string LoggerCategoryName = Name;
    public static readonly string TracingSourceName = Name;
    public static readonly string MeterName = Name;
}
