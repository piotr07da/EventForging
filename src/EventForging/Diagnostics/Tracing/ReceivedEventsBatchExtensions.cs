using System.Diagnostics;
using EventForging.EventsHandling;

namespace EventForging.Diagnostics.Tracing;

public static class ReceivedEventsBatchExtensions
{
    public static async Task IterateWithTracingRestoreAsync(this ReceivedEventsBatch batch, string activityNameSuffix, Func<ReceivedEvent, Task> onIterateAsync)
    {
        await batch.IterateWithTracingRestoreAsync(ActivitySourceProvider.ActivitySource, activityNameSuffix, onIterateAsync);
    }

    public static async Task IterateWithTracingRestoreAsync(this ReceivedEventsBatch batch, ActivitySource activitySource, string activityName, Func<ReceivedEvent, Task> onIterateAsync)
    {
        var lastActivityId = null as string;
        var lastActivity = null as Activity;

        foreach (var receivedEvent in batch)
        {
            receivedEvent.EventInfo.CustomProperties.TryGetValue(EventForgingCustomPropertyNames.ActivityId, out var activityId);
            if (!string.IsNullOrEmpty(activityId) && activityId != lastActivityId)
            {
                lastActivityId = activityId;
                lastActivity?.Complete();
                var activityContext = receivedEvent.EventInfo.RestoreActivityContext();
                lastActivity = activitySource.StartActivity(activityName, ActivityKind.Consumer, activityContext);
            }

            var catchedException = null as Exception;
            try
            {
                await onIterateAsync(receivedEvent);
            }
            catch (Exception ex)
            {
                catchedException = ex;
                lastActivity?.RecordException(ex);
                throw;
            }
            finally
            {
                if (catchedException is not null)
                {
                    lastActivity?.Complete();
                }
            }
        }

        lastActivity?.Complete();
    }
}
