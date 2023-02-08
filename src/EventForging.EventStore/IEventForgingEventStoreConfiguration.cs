namespace EventForging.EventStore;

public interface IEventForgingEventStoreConfiguration
{
    string? ConnectionString { get; set; }
    IdempotencyMode IdempotencyMode { get; set; }
}
