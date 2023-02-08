namespace EventForging.EventStore;

public interface IEventForgingEventStoreConfiguration
{
    string? Address { get; set; }
    IdempotencyMode IdempotencyMode { get; set; }
}
