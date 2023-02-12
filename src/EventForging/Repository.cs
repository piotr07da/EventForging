namespace EventForging;

internal sealed class Repository<TAggregate> : IRepository<TAggregate>
    where TAggregate : class, IEventForged
{
    private readonly IEventDatabase _database;

    public Repository(IEventDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await GetAsync(aggregateId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TAggregate> GetAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        var aggregate = AggregateProxyGenerator.Create<TAggregate>();
        var callback = new AggregateRehydrationEventDatabaseReadCallback(aggregate);
        await _database.ReadAsync<TAggregate>(aggregateId, callback, cancellationToken).ConfigureAwait(false);
        var rehydrated = callback.Rehydrated;
        if (!rehydrated)
        {
            throw new Exception($"{typeof(TAggregate).Name} with id {aggregateId} not found.");
        }

        return aggregate;
    }

    public async Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, CancellationToken cancellationToken = default)
    {
        await SaveAsync(aggregateId.ToString(), aggregate, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, CancellationToken cancellationToken = default)
    {
        var aggregateMetadata = aggregate.GetAggregateMetadata();
        var lastReadAggregateVersion = aggregateMetadata.ReadVersion;

        if (expectedVersion.IsNone && lastReadAggregateVersion.AggregateExists)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, lastReadAggregateVersion, null);
        }

        if (expectedVersion.IsDefined && (lastReadAggregateVersion.AggregateDoesNotExist || expectedVersion != lastReadAggregateVersion))
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, lastReadAggregateVersion, null);
        }

        var newEvents = aggregate.Events.Get().ToArray();
        customProperties = customProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // clone
        customProperties ??= new Dictionary<string, string>();
        await _database.WriteAsync<TAggregate>(aggregateId, newEvents, lastReadAggregateVersion, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken).ConfigureAwait(false);
    }
}
