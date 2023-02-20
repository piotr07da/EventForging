namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingConfiguration : IEventStoreEventForgingConfiguration
{
    public string? Address { get; set; }
}
