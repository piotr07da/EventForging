using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace EventForging.Caching;

internal sealed class EventStreamCacheReadSession<TAggregate> : IEventStreamCacheReadSession
{
    private readonly string _aggregateId;
    private readonly IEventStreamCacheReadSession _innerSession;
    private readonly ILogger _logger;

    public EventStreamCacheReadSession(
        string aggregateId,
        IEventStreamCacheReadSession innerSession,
        ILogger logger)
    {
        _aggregateId = aggregateId;
        _innerSession = innerSession;
        _logger = logger;
    }

    public AggregateVersion Version => _innerSession.Version;

    public async IAsyncEnumerable<object> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var eventCount = 0L;
        await using var events = _innerSession.GetEventsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (await MoveNextAsync(events, cancellationToken).ConfigureAwait(false))
        {
            ++eventCount;
            yield return events.Current;
        }

        if (eventCount != Version.Next().Value)
        {
            _logger.LogWarning(
                "The event stream cache returned a number of events inconsistent with its version. Aggregate '{AggregateId}' of type '{AggregateType}' will be read from the event database.",
                _aggregateId,
                typeof(TAggregate).Name);
            throw new EventStreamCacheReadException("The event stream cache returned a number of events inconsistent with its version.");
        }
    }

    private async Task<bool> MoveNextAsync(
        IAsyncEnumerator<object> events,
        CancellationToken cancellationToken)
    {
        try
        {
            return await events.MoveNextAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            throw CreateReadException(ex);
        }
    }

    private EventStreamCacheReadException CreateReadException(Exception exception)
    {
        _logger.LogWarning(exception, "Cannot stream aggregate '{AggregateId}' of type '{AggregateType}' from the event stream cache. The aggregate will be read from the event database.", _aggregateId, typeof(TAggregate).Name);
        return new EventStreamCacheReadException(exception);
    }

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
