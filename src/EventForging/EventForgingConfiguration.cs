using EventForging.Serialization;

namespace EventForging;

public class EventForgingConfiguration : IEventForgingConfiguration
{
    internal const bool DefaultForApplyMethodsRequiredForAllAppliedEvents = true;

    public IEventForgingSerializationConfiguration Serialization { get; } = new EventForgingSerializationConfiguration();
    public bool IdempotencyEnabled { get; set; } = true;
    public bool ApplyMethodsRequiredForAllAppliedEvents { get; set; } = DefaultForApplyMethodsRequiredForAllAppliedEvents;
}
