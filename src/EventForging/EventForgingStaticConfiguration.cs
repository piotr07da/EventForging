using EventForging.Configuration;

namespace EventForging;

internal static class EventForgingStaticConfigurationProvider
{
    public static bool ApplyMethodsRequiredForAllAppliedEvents { get; set; } = EventForgingConfiguration.DefaultForApplyMethodsRequiredForAllAppliedEvents;
}
