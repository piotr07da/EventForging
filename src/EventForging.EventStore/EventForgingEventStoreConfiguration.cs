namespace EventForging.EventStore;

internal sealed class EventForgingEventStoreConfiguration : IEventForgingEventStoreConfiguration
{
    public string? ConnectionString { get; set; }
    public IdempotencyMode IdempotencyMode { get; set; } = IdempotencyMode.BasedOnInitiatorId;
}
