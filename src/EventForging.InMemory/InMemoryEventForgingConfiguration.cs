namespace EventForging.InMemory;

internal sealed class InMemoryEventForgingConfiguration : IInMemoryEventForgingConfiguration
{
    private readonly HashSet<string> _subscriptions = new();

    public bool SerializationEnabled { get; set; }
    public IReadOnlyList<string> EventSubscriptions => new List<string>(_subscriptions);

    public void AddEventSubscription(string subscriptionName)
    {
        _subscriptions.Add(subscriptionName);
    }
}
