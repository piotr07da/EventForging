using EventForging.EventStore.EventHandling;

namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingConfiguration : IEventStoreEventForgingConfiguration
{
    private readonly List<SubscriptionConfiguration> _subscriptions = new();

    public string? Address { get; set; }
    public IReadOnlyList<SubscriptionConfiguration> Subscriptions => _subscriptions;
    public IStreamNameFactory? CustomStreamNameFactory { get; private set; }

    public void SetStreamNameFactory(IStreamNameFactory streamNameFactory)
    {
        CustomStreamNameFactory = streamNameFactory;
    }

    public void SetStreamNameFactory(Func<Type, string, string> streamNameFactory)
    {
        CustomStreamNameFactory = new DelegateStreamNameFactory(streamNameFactory);
    }

    public void AddEventsSubscription(string subscriptionName, string streamName, string groupName, ulong? startFrom = 0)
    {
        _subscriptions.Add(new SubscriptionConfiguration(subscriptionName, streamName, groupName, startFrom));
    }
}
