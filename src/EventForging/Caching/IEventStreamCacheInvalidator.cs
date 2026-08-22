namespace EventForging.Caching;

/// <summary>Removes event streams from the cache.</summary>
public interface IEventStreamCacheInvalidator
{
    /// <summary>Invalidates the cached stream for an aggregate.</summary>
    Task InvalidateAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);
}
