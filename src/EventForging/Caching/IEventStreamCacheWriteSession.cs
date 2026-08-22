namespace EventForging.Caching;

/// <summary>Receives consecutive events read from one aggregate event stream and writes them to a cache.</summary>
/// <remarks>
/// Implementations may store events before <see cref="CompleteAsync"/> is called, but readers must not see them before the
/// write is completed. A completed write must not replace cached events with an older version.
/// </remarks>
public interface IEventStreamCacheWriteSession
{
    /// <summary>Appends an event together with its absolute version in the aggregate event stream.</summary>
    Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default);

    /// <summary>Completes the cache write after all events returned by the database have been read.</summary>
    Task CompleteAsync(CancellationToken cancellationToken = default);
}
