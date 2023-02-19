using EventForging.Serialization;

namespace EventForging;

public class EventForgingConfiguration : IEventForgingConfiguration
{
    public IEventForgingSerializationConfiguration Serialization { get; } = new EventForgingSerializationConfiguration();
    public bool IdempotencyEnabled { get; set; } = true;
}
