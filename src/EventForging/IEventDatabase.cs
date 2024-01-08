namespace EventForging;

public interface IEventDatabase
{
    IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);
    Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default);
}
