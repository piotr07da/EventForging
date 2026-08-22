using System.Runtime.CompilerServices;

namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheReadSession : IEventStreamCacheReadSession
{
    private readonly object[] _events;

    private MemoryEventStreamCacheReadSession(object[] events, AggregateVersion version)
    {
        _events = events;
        Version = version;
    }

    internal static MemoryEventStreamCacheReadSession? TryCreate(
        MemoryEventStreamCache cache,
        MemoryEventStreamCacheKey cacheKey)
    {
        return cache.TryGetSnapshot(cacheKey, out var snapshot)
            ? new MemoryEventStreamCacheReadSession(snapshot.Events, snapshot.Version)
            : null;
    }

    public AggregateVersion Version { get; }

    public async IAsyncEnumerable<object> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var eventIndex = 0; eventIndex < _events.Length; ++eventIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return _events[eventIndex];
        }
    }
}
