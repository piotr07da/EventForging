using EventForging.Serialization;

namespace EventForging;

internal class EventForgingConfiguration : IEventForgingConfiguration
{
    internal const bool DefaultForApplyMethodsRequiredForAllAppliedEvents = true;

    internal EventForgingConfiguration(
        IEventForgingSerializationConfiguration serialization)
    {
        Serialization = serialization;
    }

    public IEventForgingSerializationConfiguration Serialization { get; }
    public bool IdempotencyEnabled { get; set; } = true;
    public bool ApplyMethodsRequiredForAllAppliedEvents { get; set; } = DefaultForApplyMethodsRequiredForAllAppliedEvents;
}
