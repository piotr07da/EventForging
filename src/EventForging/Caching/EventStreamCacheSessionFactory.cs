using EventForging.Diagnostics.Logging;
using EventForging.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace EventForging.Caching;

internal sealed class EventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
{
    private readonly IEventStreamCacheSessionFactory? _innerSessionFactory;
    private readonly IEventStreamCacheConfiguration? _configuration;
    private readonly ILogger _logger;

    public EventStreamCacheSessionFactory(
        IEnumerable<IEventStreamCacheSessionFactory> eventStreamCacheSessionFactories,
        IEnumerable<IEventStreamCacheConfiguration> eventStreamCacheConfigurations,
        IEventForgingLoggerProvider loggerProvider)
    {
        _innerSessionFactory = eventStreamCacheSessionFactories.SingleOrDefault();
        _configuration = eventStreamCacheConfigurations.SingleOrDefault();
        _logger = loggerProvider.Logger;

        var aggregateCachingRatio = _configuration?.AggregateCachingRatio ?? 1d;
        if (!(aggregateCachingRatio >= 0d && aggregateCachingRatio <= 1d))
        {
            throw new EventForgingConfigurationException($"{nameof(IEventStreamCacheConfiguration.AggregateCachingRatio)} must be between zero and one.");
        }
    }

    public async Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        var innerSessionFactory = _innerSessionFactory;
        if (innerSessionFactory is null || !IsAggregateEligible(aggregateId))
        {
            return null;
        }

        try
        {
            var eventStreamCacheReadSession = await innerSessionFactory
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
        var innerSessionFactory = _innerSessionFactory;
        if (innerSessionFactory is null || !IsAggregateEligible(aggregateId))
        {
            return null;
        }

        try
        {
            var eventStreamCacheWriteSession = await innerSessionFactory
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

    private bool IsAggregateEligible(string aggregateId)
    {
        return _configuration is null
               || EventStreamCacheAggregateEligibility.IsEligible(
                   aggregateId,
                   _configuration.AggregateCachingRatio);
    }

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
