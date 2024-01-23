namespace EventForging.InMemory;

internal sealed class InMemoryEventForgingConfiguration : IInMemoryEventForgingConfiguration
{
    private readonly HashSet<string> _subscriptions = new();

    public bool SerializationEnabled { get; set; }
    public IReadOnlyList<string> EventSubscriptions => new List<string>(_subscriptions);
    public IStreamIdFactory StreamIdFactory { get; private set; } = new DefaultStreamIdFactory();

    public void AddEventSubscription(string subscriptionName)
    {
        _subscriptions.Add(subscriptionName);
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
