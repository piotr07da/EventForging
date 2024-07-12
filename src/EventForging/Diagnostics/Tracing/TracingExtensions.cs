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
        var parentActivity = Activity.Current;
        Activity? activity;
        if (parentActivity is not null)
        {
            // Since an IEventDispatcher instance can be reused inside an event handler, we need to detect such nesting and avoid creating new activities.
            // While nesting behavior could potentially be a useful and expected feature, for now, let's keep it simple and avoid cluttered tracing.
            if (parentActivity.DisplayName.StartsWith(TracingActivityNames.EventDispatcherDispatch))
            {
                return null;
            }

            // ReSharper disable once ExplicitCallerInfoArgument
            activity = activitySource.StartActivity(TracingActivityNames.EventDispatcherDispatch, ActivityKind.Consumer);
        }
        else
        {
            var parentActivityContext = GetParentActivityContext(receivedEventsBatch);
            activity = activitySource.StartActivity(TracingActivityNames.EventDispatcherDispatch, ActivityKind.Consumer, parentActivityContext);
        }

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
        if (receivedEventsBatch.Count > 0)
        {
            var firstEvent = receivedEventsBatch.First();
            return firstEvent.EventInfo.CustomProperties.RestoreActivityContext();
        }

        return default;
    }
}
