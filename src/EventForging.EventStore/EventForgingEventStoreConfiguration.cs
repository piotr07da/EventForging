namespace EventForging.EventStore;

internal sealed class EventForgingEventStoreConfiguration : IEventForgingEventStoreConfiguration
{
    public string? Address { get; set; }
}
