using EventForging.EventStore.EventHandling;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingConfiguration : IEventStoreEventForgingConfiguration
{
    private readonly List<SubscriptionConfiguration> _subscriptions = new();

    public string? Address { get; set; }
    public IReadOnlyList<SubscriptionConfiguration> Subscriptions => _subscriptions;
    public IStreamNameFactory StreamNameFactory { get; private set; } = new DefaultStreamNameFactory();

    public void AddEventsSubscription(
        string subscriptionName,
        string streamName,
        string groupName,
        PersistentSubscriptionNakEventAction eventHandlingExceptionNakAction = PersistentSubscriptionNakEventAction.Retry,
        ulong? startFrom = 0)
    {
        _subscriptions.Add(new SubscriptionConfiguration(subscriptionName, streamName, groupName, eventHandlingExceptionNakAction, startFrom));
    }

    public void SetStreamNameFactory(IStreamNameFactory streamNameFactory)
    {
        StreamNameFactory = streamNameFactory;
    }

    public void SetStreamNameFactory(Func<Type, string, string> streamNameFactory)
    {
        StreamNameFactory = new DelegateStreamNameFactory(streamNameFactory);
    }
}
