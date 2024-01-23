using EventForging.EventStore.EventHandling;
using EventStore.Client;

namespace EventForging.EventStore;

public interface IEventStoreEventForgingConfiguration
{
    string? Address { get; set; }

    IReadOnlyList<SubscriptionConfiguration> Subscriptions { get; }

    IStreamIdFactory StreamIdFactory { get; }

    void AddEventsSubscription(
        string subscriptionName,
        string streamId,
        string groupName,
        PersistentSubscriptionNakEventAction eventHandlingExceptionNakAction = PersistentSubscriptionNakEventAction.Retry,
        ulong? startFrom = 0);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.</param>
    void SetStreamIdFactory(IStreamIdFactory streamIdFactory);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.<br /> The first argument is an aggregate type.<br /> The second argument is an aggregate identifier.</param>
    void SetStreamIdFactory(Func<Type, string, string> streamIdFactory);
}
