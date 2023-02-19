namespace EventForging.EventsHandling;

public sealed class EventInfo
{
    public EventInfo(Guid eventId, long eventNumber, string eventType, Guid conversationId, Guid initiatorId, DateTime timestamp, IDictionary<string, string> customProperties)
    {
        EventId = eventId;
        EventNumber = eventNumber;
        EventType = eventType;
        ConversationId = conversationId;
        InitiatorId = initiatorId;
        Timestamp = timestamp;
        CustomProperties = customProperties;
    }

    public Guid EventId { get; }
    public long EventNumber { get; }
    public string EventType { get; }
    public Guid ConversationId { get; }
    public Guid InitiatorId { get; }
    public DateTime Timestamp { get; }
    public IDictionary<string, string> CustomProperties { get; }

    public string? TryGetPropertyValue(string propertyName) => CustomProperties.TryGetValue(propertyName, out var propertyValue) ? propertyValue : null;
}
