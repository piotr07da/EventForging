namespace EventForging.InMemory;

public interface IInMemoryEventForgingConfiguration
{
    bool SerializationEnabled { get; set; }
    IReadOnlyList<string> EventSubscriptions { get; }
    IStreamIdFactory StreamIdFactory { get; }

    void AddEventSubscription(string subscriptionName);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.</param>
    void SetStreamIdFactory(IStreamIdFactory streamIdFactory);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.<br /> The first argument is an aggregate type.<br /> The second argument is an aggregate identifier.</param>
    void SetStreamIdFactory(Func<Type, string, string> streamIdFactory);
}
