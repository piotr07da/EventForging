using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.EventsHandling;

internal sealed class EventDispatcher : IEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public EventDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task DispatchAsync(string subscriptionName, object e, EventInfo ei, CancellationToken cancellationToken)
    {
        if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));
        if (e is null) throw new ArgumentNullException(nameof(e));

        var eventType = e.GetType();

        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);

        var handleMethod = handlerType.GetMethod("Handle")!;

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
