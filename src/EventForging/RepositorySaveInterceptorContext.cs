namespace EventForging;

public sealed record RepositorySaveInterceptorContext<TAggregate>(
    string AggregateId,
    TAggregate Aggregate,
    ExpectedVersion ExpectedVersion,
    Guid ConversationId,
    Guid InitiatorId,
    IDictionary<string, string> CustomProperties);
