using Microsoft.Extensions.Logging;

namespace EventForging.Caching;

internal sealed class EventStreamCacheWriteSession<TAggregate> : IEventStreamCacheWriteSession
{
    private readonly string _aggregateId;
    private readonly IEventStreamCacheWriteSession _innerSession;
    private readonly ILogger _logger;
    private bool _isDisabled;

    public EventStreamCacheWriteSession(
        string aggregateId,
        IEventStreamCacheWriteSession innerSession,
        ILogger logger)
    {
        _aggregateId = aggregateId;
        _innerSession = innerSession;
        _logger = logger;
    }

    public async Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default)
    {
        if (_isDisabled)
        {
            return;
        }

        try
        {
            await _innerSession.AppendAsync(@event, version, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            Disable(ex);
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisabled)
        {
            return;
        }

        try
        {
            await _innerSession.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!IsRequestedCancellation(ex, cancellationToken))
        {
            Disable(ex);
        }
    }

    private void Disable(Exception exception)
    {
        _isDisabled = true;
        _logger.LogWarning(exception, "Cannot cache aggregate '{AggregateId}' of type '{AggregateType}'. This aggregate read will not update the event stream cache further.", _aggregateId, typeof(TAggregate).Name);
    }

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken)
    {
        return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
    }
}
