namespace EventForging;

/// <summary>Provides access to aggregate event streams stored in an event database.</summary>
public interface IEventDatabase
{
    /// <summary>Reads all events from an aggregate event stream in ascending version order.</summary>
    IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Reads events from an aggregate event stream starting at <paramref name="readPosition"/>, in ascending version order.</summary>
    IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, CancellationToken cancellationToken = default);

    /// <summary>Reads all event records, including their metadata, from an aggregate event stream in ascending version order.</summary>
    IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Appends events to an aggregate event stream in their supplied order.</summary>
    /// <param name="retrievedVersion">The version of the aggregate when it was retrieved.</param>
    /// <param name="conversationId">The identifier of the conversation that produced the events.</param>
    /// <param name="initiatorId">The identifier of the initiator that produced the events.</param>
    /// <param name="customProperties">Properties stored as additional event metadata.</param>
    Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default);
}
