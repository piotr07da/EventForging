using EventForging.EventStore.EventHandling;
using EventStore.Client;

namespace EventForging.EventStore;

public interface IEventStoreEventForgingConfiguration
{
    string? Address { get; set; }

    IReadOnlyList<SubscriptionConfiguration> Subscriptions { get; }

    IStreamNameFactory StreamNameFactory { get; }

    void AddEventsSubscription(
        string subscriptionName,
        string streamName,
        string groupName,
        PersistentSubscriptionNakEventAction eventHandlingExceptionNakAction = PersistentSubscriptionNakEventAction.Retry,
        ulong? startFrom = 0);

    /// <summary>
    ///     Allows to set custom stream name factory.
    /// </summary>
    /// <param name="streamNameFactory">The custom stream name factory.</param>
    void SetStreamNameFactory(IStreamNameFactory streamNameFactory);

    /// <summary>
    ///     Allows to set custom stream name factory.
    /// </summary>
    /// <param name="streamNameFactory">
    ///     The custom stream name factory.<br />
    ///     The first argument is an aggregate type.<br />
    ///     The second argument is an aggregate identifier.
    /// </param>
    void SetStreamNameFactory(Func<Type, string, string> streamNameFactory);
}
