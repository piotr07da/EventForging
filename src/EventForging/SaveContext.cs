namespace EventForging;

/// <summary>Represents the context of a repository save operation, providing details about the aggregate being saved.</summary>
/// <param name="AggregateId">ID of the aggregate being saved.</param>
/// <param name="Aggregate">The aggregate instance being saved.</param>
/// <param name="RetrievedVersion">The version of the aggregate as retrieved from the database or newly created. In other words, the version of the aggregate before applying any new events. A current version.</param>
/// <param name="ExpectedVersion">The expected version of the aggregate, as provided by the caller. This is used for optimistic concurrency checks.</param>
/// <param name="ConversationId">ID of the conversation to which this save operation belongs.</param>
/// <param name="InitiatorId">ID of the message that initiated this save operation.</param>
/// <param name="CustomProperties">A dictionary of custom properties associated with this save operation. This data will be stored alongside the events in the database as events metadata.</param>
/// <typeparam name="TAggregate">Type of the aggregate being saved.</typeparam>
public sealed record SaveContext<TAggregate>(
    string AggregateId,
    TAggregate Aggregate,
    AggregateVersion RetrievedVersion,
    ExpectedVersion ExpectedVersion,
    Guid ConversationId,
    Guid InitiatorId,
    IDictionary<string, string> CustomProperties);
