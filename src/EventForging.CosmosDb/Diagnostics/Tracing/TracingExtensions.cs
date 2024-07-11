using System.Diagnostics;
using System.Globalization;
using System.Net;
using EventForging.Diagnostics.Tracing;
using EventForging.EventsHandling;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb.Diagnostics.Tracing;

internal static class TracingExtensions
{
    internal static Activity? StartEventDatabaseReadActivity(this ActivitySource activitySource)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.EventDatabaseRead);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(CosmosDbTracingAttributeNames.DatabaseSystem, CosmosDbTracingAttributeNames.DatabaseSystemValue);

        return activity;
    }

    internal static Activity? EnrichEventDatabaseReadActivityWithStreamId(this Activity? activity, string streamId)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseRead, CosmosDbTracingAttributeNames.EventDatabaseStreamId, streamId);
    }

    internal static Activity? EnrichEventDatabaseReadActivityWithReadPageInformation(this Activity? activity, int pageCount, double totalRequestCharge)
    {
        return activity.EnrichWithTagsIfNotNull(TracingActivityNames.EventDatabaseRead, new Dictionary<string, string>
        {
            { CosmosDbTracingAttributeNames.EventDatabaseReadPageCount, pageCount.ToString() },
            { CosmosDbTracingAttributeNames.CosmosDbRequestCharge, totalRequestCharge.ToString(CultureInfo.InvariantCulture) },
        });
    }

    internal static Activity? RecordEventDatabaseReadActivityResultPageReadEvent(this Activity? activity, HttpStatusCode statusCode, double requestCharge)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(TracingActivityNames.EventDatabaseRead);

        if (!activity.IsAllDataRequested)
        {
            return activity;
        }

        activity.AddEvent(new ActivityEvent(CosmosDbTracingAttributeNames.ResultPageReadEvent.Name, DateTimeOffset.UtcNow, new ActivityTagsCollection
        {
            { CosmosDbTracingAttributeNames.DatabaseSystem, CosmosDbTracingAttributeNames.DatabaseSystemValue },
            { CosmosDbTracingAttributeNames.CosmosDbStatusCode, statusCode.ToString() },
            { CosmosDbTracingAttributeNames.CosmosDbRequestCharge, requestCharge.ToString(CultureInfo.InvariantCulture) },
        }));

        return activity;
    }

    internal static Activity? StartEventDatabaseWriteActivity(this ActivitySource activitySource, AggregateVersion retrievedVersion)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.EventDatabaseWrite);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.AggregateVersion, retrievedVersion.ToString());

        return activity;
    }

    internal static Activity? EnrichEventDatabaseWriteActivityWithStreamId(this Activity? activity, string streamId)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWrite, CosmosDbTracingAttributeNames.EventDatabaseStreamId, streamId);
    }

    internal static Activity? EnrichEventDatabaseWriteActivityWithTryCount(this Activity? activity, int tryCount)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWrite, CosmosDbTracingAttributeNames.EventDatabaseWriteAttemptCount, tryCount.ToString());
    }

    internal static Activity? StartEventDatabaseWriteAttemptActivity(this ActivitySource activitySource, AggregateVersion retrievedVersion)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.EventDatabaseWriteAttempt);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.AggregateVersion, retrievedVersion.ToString());
        activity.SetTag(CosmosDbTracingAttributeNames.DatabaseSystem, CosmosDbTracingAttributeNames.DatabaseSystemValue);

        return activity;
    }

    internal static Activity? EnrichEventDatabaseWriteAttemptActivityWithStreamId(this Activity? activity, string streamId)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWriteAttempt, CosmosDbTracingAttributeNames.EventDatabaseStreamId, streamId);
    }

    internal static Activity? EnrichEventDatabaseWriteAttemptActivityWithContainer(this Activity? activity, string container)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.EventDatabaseWriteAttempt, CosmosDbTracingAttributeNames.CosmosDbContainer, container);
    }

    internal static Activity? EnrichEventDatabaseWriteAttemptActivityWithResponse(this Activity? activity, TransactionalBatchResponse? response)
    {
        if (response is null)
        {
            return activity;
        }

        return activity.EnrichWithTagsIfNotNull(TracingActivityNames.EventDatabaseWriteAttempt, new Dictionary<string, string>
        {
            { CosmosDbTracingAttributeNames.CosmosDbStatusCode, response.StatusCode.ToString() },
            { CosmosDbTracingAttributeNames.CosmosDbRequestCharge, response.RequestCharge.ToString(CultureInfo.InvariantCulture) },
        });
    }

    internal static Activity? RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent(this Activity? activity, string eventName, HttpStatusCode statusCode, double requestCharge, IDictionary<string, string>? additionalTags = null)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(TracingActivityNames.EventDatabaseWriteAttempt);

        var tags = new ActivityTagsCollection
        {
            { CosmosDbTracingAttributeNames.DatabaseSystem, CosmosDbTracingAttributeNames.DatabaseSystemValue },
            { CosmosDbTracingAttributeNames.CosmosDbStatusCode, statusCode.ToString() },
            { CosmosDbTracingAttributeNames.CosmosDbRequestCharge, requestCharge.ToString(CultureInfo.InvariantCulture) },
        };

        if (additionalTags is not null)
        {
            foreach (var tag in additionalTags)
            {
                tags.Add(tag.Key, tag.Value);
            }
        }

        activity.AddEvent(new ActivityEvent(eventName, DateTimeOffset.UtcNow, tags));

        return activity;
    }

    internal static Activity? StartEventsSubscriberHandleChangesActivity(this ActivitySource activitySource, string subscriptionName, ReceivedEventsBatch receivedEventsBatch)
    {
        var parentActivityContext = GetParentActivityContext(receivedEventsBatch);

        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(ActivityKind.Consumer, parentActivityContext, name: TracingActivityNames.EventsSubscriberHandleChanges);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.SubscriptionName, subscriptionName);
        activity.SetTag(CosmosDbTracingAttributeNames.ChangesCount, receivedEventsBatch.Count.ToString());

        return activity;
    }

    private static ActivityContext GetParentActivityContext(ReceivedEventsBatch receivedEventsBatch)
    {
        if (receivedEventsBatch.Count > 0)
        {
            var firstEvent = receivedEventsBatch.First();
            return firstEvent.EventInfo.CustomProperties.RestoreActivityContext();
        }

        return default;
    }
}
