namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheInvalidator : IEventStreamCacheInvalidator
{
    private readonly MemoryEventStreamCache _cache;

    public MemoryEventStreamCacheInvalidator(MemoryEventStreamCache cache)
    {
        _cache = cache;
    }

    public Task InvalidateAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Invalidate<TAggregate>(aggregateId);
        return Task.CompletedTask;
    }
}
