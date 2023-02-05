namespace EventForging.InMemory;

internal sealed class EventForgingInMemoryConfiguration : IEventForgingInMemoryConfiguration
{
    public bool SerializationEnabled { get; set; }
}
