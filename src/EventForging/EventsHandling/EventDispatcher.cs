using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventForging.EventsHandling;

internal sealed class EventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public EventDispatcher(
        IServiceProvider serviceProvider,
        ILoggerFactory? loggerFactory = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = loggerFactory.CreateEventForgingLogger();
    }

    public async Task DispatchAsync(string subscriptionName, object eventData, EventInfo eventInfo, CancellationToken cancellationToken)
    {
        await DispatchAsync(subscriptionName, new ReceivedEvent(eventData, eventInfo), cancellationToken);
    }

    public async Task DispatchAsync(string subscriptionName, ReceivedEvent receivedEvent, CancellationToken cancellationToken)
    {
        await DispatchAsync(subscriptionName, new ReceivedEventsBatch(new[] { receivedEvent, }), cancellationToken);
    }

    public async Task DispatchAsync(string subscriptionName, ReceivedEventsBatch receivedEventsBatch, CancellationToken cancellationToken)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

        if (receivedEventsBatch.Count == 0)
        {
            return;
        }

        await DispatchToEventBatchHandlersAsync(subscriptionName, receivedEventsBatch, cancellationToken);

        foreach (var receivedEvent in receivedEventsBatch)
        {
            var ed = receivedEvent.EventData;
            var ei = receivedEvent.EventInfo;
            try
            {
                await DispatchToAnyEventHandlersAsync(subscriptionName, ed, ei, cancellationToken);
                await DispatchToGenericEventHandlersAsync(subscriptionName, ed, ei, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching the {EventName} event to the event handlers. Stream id is {StreamId}. Event number is {EventNumber}.", ei.EventType, ei.StreamId, ei.EventNumber);
                throw;
            }
        }
    }

    private async Task DispatchToEventBatchHandlersAsync(string subscriptionName, ReceivedEventsBatch receivedEventsBatch, CancellationToken cancellationToken)
    {
        var handlers = _serviceProvider.GetServices<IEventBatchHandler>();

        foreach (var handler in handlers)
        {
            if (!handler.SubscriptionName.Equals(subscriptionName))
            {
                continue;
            }

            await handler.HandleAsync(receivedEventsBatch, cancellationToken);
        }
    }

    private async Task DispatchToAnyEventHandlersAsync(string subscriptionName, object e, EventInfo ei, CancellationToken cancellationToken)
    {
        var handlers = _serviceProvider.GetServices<IAnyEventHandler>();

        foreach (var handler in handlers)
        {
            if (!handler.SubscriptionName.Equals(subscriptionName))
            {
                continue;
            }

            await handler.HandleAsync(e, ei, cancellationToken);
        }
    }

    private async Task DispatchToGenericEventHandlersAsync(string subscriptionName, object e, EventInfo ei, CancellationToken cancellationToken)
    {
        var eventType = e.GetType();

        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handleMethod = handlerType.GetMethod(nameof(IEventHandler<object>.HandleAsync))!;

        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (!(handler as IEventHandler)!.SubscriptionName.Equals(subscriptionName))
            {
                continue;
            }

            try
            {
                var handleTask = handleMethod.Invoke(handler, new[] { e, ei, cancellationToken, }) as Task;
                await handleTask!;
            }
            catch (TargetInvocationException tiEx)
            {
                if (tiEx.InnerException != null)
                    ExceptionDispatchInfo.Capture(tiEx.InnerException).Throw();
                else
                    throw;
            }
        }
    }
}
