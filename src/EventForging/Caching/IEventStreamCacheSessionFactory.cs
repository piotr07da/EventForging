namespace EventForging.Caching;

/// <summary>Creates sessions that read from and write to an event stream cache.</summary>
public interface IEventStreamCacheSessionFactory
{
    /// <summary>Tries to create a session for reading a complete cached event stream.</summary>
    /// <returns>A read session, or <see langword="null"/> when the stream is not cached.</returns>
    Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default);

    /// <summary>Tries to create a session that receives events while an aggregate event stream is being read.</summary>
    /// <returns>A write session, or <see langword="null"/> when the cache declines to store the stream.</returns>
    Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default);
}
