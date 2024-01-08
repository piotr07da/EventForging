namespace EventForging;

public sealed record EventDatabaseRecord(
    Guid EventId,
    long EventNumber,
    string EventType,
    DateTime Timestamp,
    object EventData,
    Guid ConversationId,
    Guid InitiatorId,
    IDictionary<string, string> CustomProperties);
