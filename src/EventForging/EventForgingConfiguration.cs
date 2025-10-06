using EventForging.Serialization;

namespace EventForging.Configuration;

internal class EventForgingConfiguration : IEventForgingConfiguration
{
    internal const bool DefaultForApplyMethodsRequiredForAllAppliedEvents = true;

    internal EventForgingConfiguration(
        IEventForgingSerializationConfiguration serialization,
        IEventForgingRepositoryInterceptorsConfiguration repositoryInterceptors)
    {
        Serialization = serialization;
        RepositoryInterceptors = repositoryInterceptors;
    }

    public IEventForgingSerializationConfiguration Serialization { get; }
    public IEventForgingRepositoryInterceptorsConfiguration RepositoryInterceptors { get; }
    public bool IdempotencyEnabled { get; set; } = true;
    public bool ApplyMethodsRequiredForAllAppliedEvents { get; set; } = DefaultForApplyMethodsRequiredForAllAppliedEvents;
}
