using EventForging.Diagnostics.Logging;
using EventForging.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace EventForging.Caching;

internal sealed class EventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
{
    private readonly IEventStreamCacheSessionFactory? _innerSessionFactory;
    private readonly ILogger _logger;

    public EventStreamCacheSessionFactory(
        IEnumerable<IEventStreamCacheSessionFactory> eventStreamCacheSessionFactories,
        IEventForgingLoggerProvider loggerProvider)
    {
        _innerSessionFactory = eventStreamCacheSessionFactories.SingleOrDefault();
        _logger = loggerProvider.Logger;
    }

    public async Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        if (_innerSessionFactory is null)
        {
            return null;
        }

        try
        {
            var eventStreamCacheReadSession = await _innerSessionFactory
                .TryCreateReadSessionAsync<TAggregate>(aggregateId, cancellationToken)
                .ConfigureAwait(false);
            if (eventStreamCacheReadSession is null)
            {
                EventStreamReadMetrics.RecordCacheLookupMiss(typeof(TAggregate));
            }
            else
            {
                EventStreamReadMetrics.RecordCacheLookupHit(typeof(TAggregate));
                eventStreamCacheReadSession = new EventStreamCacheReadSession<TAggregate>(
                    aggregateId,
                    eventStreamCacheReadSession,
                    _logger);
            }

            return eventStreamCacheReadSession;
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            _logger.LogWarning(
                ex,
                "Cannot read aggregate '{AggregateId}' of type '{AggregateType}' from the event stream cache. The aggregate will be read from the event database.",
                aggregateId,
                typeof(TAggregate).Name);
            EventStreamReadMetrics.RecordCacheLookupMiss(typeof(TAggregate));
            return null;
        }
    }

    public async Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        if (_innerSessionFactory is null)
        {
            return null;
        }

        try
        {
            var eventStreamCacheWriteSession = await _innerSessionFactory
                .TryCreateWriteSessionAsync<TAggregate>(aggregateId, cancellationToken)
                .ConfigureAwait(false);
            return eventStreamCacheWriteSession is null
                ? null
                : new EventStreamCacheWriteSession<TAggregate>(
                    aggregateId,
                    eventStreamCacheWriteSession,
                    _logger);
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Cannot start caching aggregate '{AggregateId}' of type '{AggregateType}'.", aggregateId, typeof(TAggregate).Name);
            return null;
        }
    }

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
