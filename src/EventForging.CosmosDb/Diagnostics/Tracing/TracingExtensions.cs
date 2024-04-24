using System.Diagnostics;
using EventForging.Diagnostics.Tracing;

namespace EventForging.CosmosDb.Diagnostics.Tracing;

public static class TracingExtensions
{
    public static Activity? StartEventDatabaseWriteActivity(this ActivitySource activitySource)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.EventDatabaseWrite);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(CosmosDbTracingAttributeNames.DatabaseSystem, "cosmosdb");

        return activity;
    }

    public static Activity? EnrichEventDatabaseWriteActivityWithStreamId(this Activity? activity, string streamId)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWrite, CosmosDbTracingAttributeNames.EventDatabaseStreamId, streamId);
    }

    public static Activity? EnrichEventDatabaseWriteActivityWithTryCount(this Activity? activity, int tryCount)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWrite, CosmosDbTracingAttributeNames.EventDatabaseTryCount, tryCount.ToString());
    }

    public static Activity? RecordEventDatabaseWriteTryEvent(this Activity? activity)
    {
        return activity;
    }
}
