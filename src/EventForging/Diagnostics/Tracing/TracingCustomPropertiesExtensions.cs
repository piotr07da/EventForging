using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

public static class TracingCustomPropertiesExtensions
{
    public static void StoreCurrentActivityId(this IDictionary<string, string> customProperties)
    {
        var activity = Activity.Current;
        if (activity?.Id != null)
        {
            customProperties[EventForgingCustomPropertyNames.ActivityId] = activity.Id;
        }
    }

    public static ActivityContext RestoreActivityContext(this IDictionary<string, string> customProperties)
    {
        if (customProperties.TryGetValue(EventForgingCustomPropertyNames.ActivityId, out var activityId))
        {
            if (ActivityContext.TryParse(activityId, null, true, out var parentContext))
            {
                return parentContext;
            }
        }

        return default;
    }
}
