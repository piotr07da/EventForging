using EventForging.EventStore.EventHandling;

namespace EventForging.EventStore;

public interface IEventStoreEventForgingConfiguration
{
    string? Address { get; set; }

    IReadOnlyList<SubscriptionConfiguration> Subscriptions { get; }

    IStreamNameFactory? CustomStreamNameFactory { get; }

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

    void AddEventsSubscription(string subscriptionName, string streamName, string groupName, ulong? startFrom = 0);
}
