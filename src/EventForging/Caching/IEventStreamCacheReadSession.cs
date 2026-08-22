namespace EventForging.Caching;

/// <summary>Reads events from one cached aggregate event stream at a fixed version.</summary>
public interface IEventStreamCacheReadSession
{
    /// <summary>The version of the last event available through this session.</summary>
    AggregateVersion Version { get; }

    /// <summary>Gets all cached events from version zero through <see cref="Version"/> in ascending version order.</summary>
    IAsyncEnumerable<object> GetEventsAsync(CancellationToken cancellationToken = default);
}
