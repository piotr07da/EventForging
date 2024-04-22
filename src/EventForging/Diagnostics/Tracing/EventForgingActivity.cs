using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

public sealed class EventForgingActivity
{
    public EventForgingActivity(Activity activity)
    {
        Activity = activity;
    }

    public Activity Activity { get; }

    public static EventForgingActivity? Current => Activity.Current == null ? null : new EventForgingActivity(Activity.Current);

    public void AssertName(string name)
    {
        if (Activity.OperationName != name)
        {
            throw new InvalidOperationException($"Activity name '{Activity.OperationName}' is not '{name}'.");
        }
    }

    public void RecordException(Exception exception)
    {
        var exceptionMessage = exception.Message;

        var exceptionEventTags = new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().Name },
            { "exception.message", exceptionMessage },
        };

        var exceptionEvent = new ActivityEvent("exception", DateTimeOffset.UtcNow, exceptionEventTags);

        Activity.AddEvent(exceptionEvent);
        Activity.SetStatus(ActivityStatusCode.Error, exceptionMessage);
    }

    public void Complete()
    {
        if (Activity.Status == ActivityStatusCode.Unset)
            Activity.SetStatus(ActivityStatusCode.Ok);

        Activity.Dispose();
    }
}
