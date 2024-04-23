using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

public static class ActivityExtensions
{
    public static void RecordException(this Activity activity, Exception exception, bool escaped = true)
    {
        var exceptionMessage = exception.Message;

        var exceptionEventTags = new ActivityTagsCollection
        {
            { TracingAttributeNames.ExceptionEvent.ExceptionEscaped, escaped.ToString().ToLower() },
            { TracingAttributeNames.ExceptionEvent.ExceptionType, exception.GetType().Name },
            { TracingAttributeNames.ExceptionEvent.ExceptionMessage, exceptionMessage },
            { TracingAttributeNames.ExceptionEvent.ExceptionStackTrace, exception.StackTrace },
        };

        var exceptionEvent = new ActivityEvent(TracingAttributeNames.ExceptionEvent.Name, DateTimeOffset.UtcNow, exceptionEventTags);

        activity.AddEvent(exceptionEvent);
        activity.SetStatus(ActivityStatusCode.Error, exceptionMessage);
    }

    public static void Complete(this Activity activity)
    {
        if (activity.Status == ActivityStatusCode.Unset)
            activity.SetStatus(ActivityStatusCode.Ok);

        activity.Dispose();
    }

    public static void AssertName(this Activity activity, string name)
    {
        if (activity.OperationName != name)
        {
            throw new InvalidOperationException($"Activity name '{activity.OperationName}' is not '{name}'.");
        }
    }
}
