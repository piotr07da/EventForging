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

    public static Activity? EnrichWithTagsIfNotNull(this Activity? activity, string expectedActivityName, IDictionary<string, string> tags)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(expectedActivityName);

        foreach (var tag in tags)
        {
            var tagName = tag.Key;
            var tagValue = tag.Value;
            activity.SetTag(tagName, tagValue);
        }

        return activity;
    }

    public static Activity? EnrichWithTagIfNotNull(this Activity? activity, string expectedActivityName, string tagName, string tagValue)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(expectedActivityName);
        activity.SetTag(tagName, tagValue);
        return activity;
    }

    public static void AssertName(this Activity activity, string name)
    {
        if (activity.OperationName != name)
        {
            throw new InvalidOperationException($"Activity name '{activity.OperationName}' is not '{name}'.");
        }
    }
}
