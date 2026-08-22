using EventForging.Diagnostics.Logging;
using Microsoft.Extensions.Logging;

namespace EventForging.Caching;

internal sealed class EventStreamCacheInvalidator : IEventStreamCacheInvalidator
{
    private readonly IEventStreamCacheInvalidator? _innerEventStreamCacheInvalidator;
    private readonly ILogger _logger;

    public EventStreamCacheInvalidator(
        IEnumerable<IEventStreamCacheInvalidator> eventStreamCacheInvalidators,
        IEventForgingLoggerProvider loggerProvider)
    {
        _innerEventStreamCacheInvalidator = eventStreamCacheInvalidators.SingleOrDefault();
        _logger = loggerProvider.Logger;
    }

    public async Task InvalidateAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        if (_innerEventStreamCacheInvalidator is null)
        {
            return;
        }

        try
        {
            await _innerEventStreamCacheInvalidator
                .InvalidateAsync<TAggregate>(aggregateId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            _logger.LogWarning(
                ex,
                "Cannot invalidate the event stream cache for aggregate '{AggregateId}' of type '{AggregateType}'.",
                aggregateId,
                typeof(TAggregate).Name);
        }
    }

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
