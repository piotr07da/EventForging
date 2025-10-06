using System.Diagnostics;

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

    internal static Activity? EnrichRepositorySaveActivityWithInterceptionPipelineStatus(this Activity? activity, string status)
    {
        return activity.EnrichWithTagIfNotNull(TracingActivityNames.RepositorySave, TracingAttributeNames.SaveInterceptionPipeline, status);
    }
}
