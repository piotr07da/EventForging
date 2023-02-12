namespace EventForging;

public interface IRepository<TAggregate>
{
    Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<TAggregate> GetAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);
    Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);
}
