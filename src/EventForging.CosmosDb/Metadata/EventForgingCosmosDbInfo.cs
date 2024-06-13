namespace EventForging.CosmosDb.Metadata;

public static class EventForgingCosmosDbInfo
{
    public static readonly string Name = "EventForging.CosmosDb";
    public static readonly string Version = typeof(CosmosDbEventDatabase).Assembly.GetName().Version?.ToString() ?? string.Empty;
}
