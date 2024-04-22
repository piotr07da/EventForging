namespace EventForging.Metadata;

public static class EventForgingInfo
{
    public static readonly string Name = "EventForging";
    public static readonly string Version = typeof(Repository<>).Assembly.GetName().Version?.ToString() ?? string.Empty;
}
