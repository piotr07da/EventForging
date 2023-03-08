namespace EventForging.InMemory;

internal sealed class InMemoryEventForgingConfiguration : IInMemoryEventForgingConfiguration
{
    private readonly HashSet<string> _subscriptions = new();

    public bool SerializationEnabled { get; set; }
    public IReadOnlyList<string> EventSubscriptions => new List<string>(_subscriptions);
    public IStreamNameFactory StreamNameFactory { get; private set; } = new DefaultStreamNameFactory();

    public void AddEventSubscription(string subscriptionName)
    {
        _subscriptions.Add(subscriptionName);
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
