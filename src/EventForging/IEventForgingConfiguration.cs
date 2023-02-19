using EventForging.Serialization;

namespace EventForging;

public interface IEventForgingConfiguration
{
    IEventForgingSerializationConfiguration Serialization { get; }
    bool IdempotencyEnabled { get; set; }
}
