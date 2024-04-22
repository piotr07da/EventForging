using System.Diagnostics;

namespace EventForging.Diagnostics.Tracing;

public static class TracingExtensions
{
    private const string RepositoryGetActivityName = "repository.get";
    private const string RepositorySaveActivityName = "repository.save";

    public static EventForgingActivity? StartRepositoryGetActivity<TAggregate>(this ActivitySource activitySource, string aggregateId, bool nullExpected)
        where TAggregate : class, IEventForged
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(RepositoryGetActivityName);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("aggregate.id", aggregateId);
        activity.SetTag("aggregate.type", typeof(TAggregate).Name);
        activity.SetTag("null_expected", nullExpected.ToString());

        return new EventForgingActivity(activity);
    }

    public static EventForgingActivity? EnrichRepositoryGetActivityWithAggregateVersion(this EventForgingActivity? activity, AggregateVersion aggregateVersion)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(RepositoryGetActivityName);

        activity.Activity.SetTag("aggregate.version", aggregateVersion.ToString());

        return activity;
    }

    public static EventForgingActivity? StartRepositorySaveActivity<TAggregate>(this ActivitySource activitySource, string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties)
        where TAggregate : class, IEventForged
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        var activity = activitySource.StartActivity(RepositorySaveActivityName);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("aggregate.id", aggregateId);
        activity.SetTag("aggregate.type", typeof(TAggregate).Name);
        activity.SetTag("aggregate.number_of_events_to_save", aggregate.Events.Count.ToString());
        activity.SetTag("expected_version", expectedVersion.ToString());
        activity.SetTag("conversation_id", conversationId.ToString());
        activity.SetTag("initiator_id", initiatorId.ToString());

        if (activity.IsAllDataRequested)
        {
            if (customProperties != null)
            {
                foreach (var customProperty in customProperties)
                {
                    activity.SetTag($"custom_property.{customProperty.Key}", customProperty.Value);
                }
            }
        }

        return new EventForgingActivity(activity);
    }

    public static EventForgingActivity? EnrichRepositorySaveActivityWithAggregateVersion(this EventForgingActivity? activity, AggregateVersion aggregateVersion)
    {
        if (activity is null)
        {
            return null;
        }

        activity.AssertName(RepositorySaveActivityName);

        activity.Activity.SetTag("aggregate.version", aggregateVersion.ToString());

        return activity;
    }
}
