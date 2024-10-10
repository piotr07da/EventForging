using System.Diagnostics;
using EventForging.EventsHandling;

namespace EventForging.Diagnostics.Tracing;

public static class EventInfoExtensions
{
    public static ActivityContext RestoreActivityContext(EventInfo eventInfo)
    {
        return eventInfo.CustomProperties.RestoreActivityContext();
    }
}
