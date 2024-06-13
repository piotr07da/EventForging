namespace EventForging.InMemory.Metadata;

public static class EventForgingInMemoryInfo
{
    public static readonly string Name = "EventForging.InMemory";
    public static readonly string Version = typeof(InMemoryEventDatabase).Assembly.GetName().Version?.ToString() ?? string.Empty;
}
