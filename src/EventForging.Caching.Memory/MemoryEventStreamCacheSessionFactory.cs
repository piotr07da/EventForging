namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
{
    private readonly MemoryEventStreamCache _cache;
    private readonly IMemoryEventStreamCacheConfiguration _configuration;

    public MemoryEventStreamCacheSessionFactory(
        MemoryEventStreamCache cache,
        IMemoryEventStreamCacheConfiguration configuration)
    {
        _cache = cache;
        _configuration = configuration;
    }

    public Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IEventStreamCacheReadSession?>(
            MemoryEventStreamCacheReadSession.TryCreate(
                _cache,
                new MemoryEventStreamCacheKey(typeof(TAggregate), aggregateId)));
    }

    public Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IEventStreamCacheWriteSession?>(
            new MemoryEventStreamCacheWriteSession(
                _cache,
                new MemoryEventStreamCacheKey(typeof(TAggregate), aggregateId),
                _configuration.MaximumTotalCachedEventCount));
    }
}
