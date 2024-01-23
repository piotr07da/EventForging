namespace EventForging.EventsHandling;

public sealed record EventInfo(string StreamId, Guid EventId, long EventNumber, string EventType, Guid ConversationId, Guid InitiatorId, DateTime Timestamp, IDictionary<string, string> CustomProperties)
{
    public string? TryGetPropertyValue(string propertyName)
    {
        return CustomProperties.TryGetValue(propertyName, out var propertyValue) ? propertyValue : null;
    }
}
