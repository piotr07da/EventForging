namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheWriteSession : IEventStreamCacheWriteSession
{
    private readonly MemoryEventStreamCache _cache;
    private readonly MemoryEventStreamCacheKey _cacheKey;
    private readonly int _maximumTotalCachedEventCount;
    private readonly List<object> _bufferedEvents = new();
    private AggregateVersion? _firstEventVersion;
    private AggregateVersion? _lastEventVersion;
    private bool _streamExceedsTotalEventCapacity;
    private bool _completed;

    public MemoryEventStreamCacheWriteSession(
        MemoryEventStreamCache cache,
        MemoryEventStreamCacheKey cacheKey,
        int maximumTotalCachedEventCount)
    {
        _cache = cache;
        _cacheKey = cacheKey;
        _maximumTotalCachedEventCount = maximumTotalCachedEventCount;
    }

    public Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_completed)
        {
            throw new EventForgingException("Cannot append an event to a completed memory event stream cache write session.");
        }

        if (_lastEventVersion is not null && version != _lastEventVersion.Value.Next())
        {
            throw new EventForgingException("Memory event stream cache write session received non-consecutive event versions.");
        }

        _firstEventVersion ??= version;
        _lastEventVersion = version;
        if (!_streamExceedsTotalEventCapacity)
        {
            if (version.Next().Value > _maximumTotalCachedEventCount)
            {
                _bufferedEvents.Clear();
                _streamExceedsTotalEventCapacity = true;
            }
            else
            {
                _bufferedEvents.Add(@event);
            }
        }

        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_completed)
        {
            throw new EventForgingException("Memory event stream cache write session has already been completed.");
        }

        _completed = true;
        if (_firstEventVersion is not null)
        {
            var events = _streamExceedsTotalEventCapacity
                ? Array.Empty<object>()
                : TakeBufferedEvents();
            _cache.CompleteWrite(
                _cacheKey,
                _firstEventVersion.Value,
                _lastEventVersion!.Value,
                events,
                streamExceedsTotalEventCapacity: _streamExceedsTotalEventCapacity);
        }

        return Task.CompletedTask;
    }

    private object[] TakeBufferedEvents()
    {
        var bufferedEvents = _bufferedEvents.ToArray();
        _bufferedEvents.Clear();
        return bufferedEvents;
    }
}
