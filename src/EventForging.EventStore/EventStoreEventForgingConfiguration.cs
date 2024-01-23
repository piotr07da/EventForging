using EventForging.EventStore.EventHandling;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingConfiguration : IEventStoreEventForgingConfiguration
{
    private readonly List<SubscriptionConfiguration> _subscriptions = new();

    public string? Address { get; set; }
    public IReadOnlyList<SubscriptionConfiguration> Subscriptions => _subscriptions;
    public IStreamIdFactory StreamIdFactory { get; private set; } = new DefaultStreamIdFactory();

    public void AddEventsSubscription(
        string subscriptionName,
        string streamId,
        string groupName,
        PersistentSubscriptionNakEventAction eventHandlingExceptionNakAction = PersistentSubscriptionNakEventAction.Retry,
        ulong? startFrom = 0)
    {
        _subscriptions.Add(new SubscriptionConfiguration(subscriptionName, streamId, groupName, eventHandlingExceptionNakAction, startFrom));
    }

    public void SetStreamIdFactory(IStreamIdFactory streamIdFactory)
    {
        StreamIdFactory = streamIdFactory;
    }

    public void SetStreamIdFactory(Func<Type, string, string> streamIdFactory)
    {
        StreamIdFactory = new DelegateStreamIdFactory(streamIdFactory);
    }
}
