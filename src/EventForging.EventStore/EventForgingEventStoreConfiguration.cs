namespace EventForging.EventStore;

internal sealed class EventForgingEventStoreConfiguration : IEventForgingEventStoreConfiguration
{
    public string? Address { get; set; }
    public IdempotencyMode IdempotencyMode { get; set; } = IdempotencyMode.BasedOnInitiatorId;
}
