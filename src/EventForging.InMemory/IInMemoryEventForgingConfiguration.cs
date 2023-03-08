namespace EventForging.InMemory;

public interface IInMemoryEventForgingConfiguration
{
    bool SerializationEnabled { get; set; }
    IReadOnlyList<string> EventSubscriptions { get; }
    IStreamNameFactory StreamNameFactory { get; }

    void AddEventSubscription(string subscriptionName);

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
