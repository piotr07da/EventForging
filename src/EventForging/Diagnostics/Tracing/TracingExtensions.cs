using System.Diagnostics;
using EventForging.EventsHandling;

namespace EventForging.Diagnostics.Tracing;

internal static class TracingExtensions
{
    internal static Activity? StartRepositoryGetActivity<TAggregate>(this ActivitySource activitySource, string aggregateId, bool nullExpected)
        where TAggregate : class, IEventForged
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.RepositoryGet);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.AggregateId, aggregateId);
        activity.SetTag(TracingAttributeNames.AggregateType, typeof(TAggregate).Name);
        activity.SetTag(TracingAttributeNames.NullExpected, nullExpected.ToString());

        return activity;
    }

    internal static Activity? EnrichRepositoryGetActivityWithAggregateVersion(this Activity? activity, AggregateVersion aggregateVersion)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.RepositoryGet, TracingAttributeNames.AggregateVersion, aggregateVersion.ToString());
    }

    internal static Activity? StartRepositorySaveActivity<TAggregate>(this ActivitySource activitySource, string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties)
        where TAggregate : class, IEventForged
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(TracingActivityNames.RepositorySave);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.AggregateId, aggregateId);
        activity.SetTag(TracingAttributeNames.AggregateType, typeof(TAggregate).Name);
        activity.SetTag(TracingAttributeNames.AggregateEventsCount, aggregate.Events.Count.ToString());
        activity.SetTag(TracingAttributeNames.ExpectedVersion, expectedVersion.ToString());
        activity.SetTag(TracingAttributeNames.ConversationId, conversationId.ToString());
        activity.SetTag(TracingAttributeNames.InitiatorId, initiatorId.ToString());

        if (activity.IsAllDataRequested)
        {
            if (customProperties != null)
            {
                foreach (var customProperty in customProperties)
                {
                    activity.SetTag($"{TracingAttributeNames.CustomPropertyPrefix}{customProperty.Key}", customProperty.Value);
                }
            }
        }

        return activity;
    }

    internal static Activity? EnrichRepositorySaveActivityWithAggregateVersion(this Activity? activity, AggregateVersion aggregateVersion)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.RepositorySave, TracingAttributeNames.AggregateVersion, aggregateVersion.ToString());
    }

    internal static Activity? StartEventDispatcherDispatchActivity(this ActivitySource activitySource, string subscriptionName, ReceivedEventsBatch receivedEventsBatch)
    {
        var parentActivityContext = GetParentActivityContext(receivedEventsBatch);
        var activity = activitySource.StartActivity(TracingActivityNames.EventDispatcherDispatch, ActivityKind.Consumer, parentActivityContext);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(TracingAttributeNames.SubscriptionName, subscriptionName);
        activity.SetTag(TracingAttributeNames.EventsBatchSize, receivedEventsBatch.Count.ToString());

        return activity;
    }

    private static ActivityContext GetParentActivityContext(ReceivedEventsBatch receivedEventsBatch)
    {
        var parentActivity = Activity.Current;
        if (parentActivity is not null)
        {
            return parentActivity.Context;
        }

        if (receivedEventsBatch.Count > 0)
        {
            var firstEvent = receivedEventsBatch.First();
            return firstEvent.EventInfo.CustomProperties.RestoreActivityContext();
        }

        return default;
    }
}
