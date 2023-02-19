namespace EventForging.InMemory;

public interface IEventForgingInMemoryConfiguration
{
    public bool SerializationEnabled { get; set; }
    public IReadOnlyList<string> EventSubscriptions { get; }
    void AddEventSubscription(string subscriptionName);
}
