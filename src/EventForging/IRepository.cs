namespace EventForging;

public interface IRepository<TAggregate>
{
    /// <summary>
    /// </summary>
    /// <param name="aggregateId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// </summary>
    /// <param name="aggregateId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TAggregate> GetAsync(string aggregateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// </summary>
    /// <param name="aggregateId"></param>
    /// <param name="aggregate"></param>
    /// <param name="expectedVersion"></param>
    /// <param name="conversationId"></param>
    /// <param name="initiatorId"></param>
    /// <param name="customProperties"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// </summary>
    /// <param name="aggregateId"></param>
    /// <param name="aggregate"></param>
    /// <param name="expectedVersion"></param>
    /// <param name="conversationId"></param>
    /// <param name="initiatorId"></param>
    /// <param name="customProperties"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);
}
