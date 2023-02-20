using EventForging.EventsHandling;

namespace EventForging.InMemory.EventHandling;

internal sealed class Subscriptions : ISubscriptions
{
    private static readonly IDictionary<string, Subscription> _subscriptions = new Dictionary<string, Subscription>();

    private readonly IEventForgingInMemoryConfiguration _configuration;
    private readonly IEventDispatcher _eventDispatcher;

    public Subscriptions(IEventForgingInMemoryConfiguration configuration, IEventDispatcher eventDispatcher)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var startTasks = new List<Task>();

        foreach (var subscriptionName in _configuration.EventSubscriptions)
        {
            var subscription = new Subscription(subscriptionName, _eventDispatcher);
            _subscriptions.Add(subscriptionName, subscription);
            var startTask = subscription.StartAsync(cancellationToken);
            startTasks.Add(startTask);
        }

        await Task.WhenAll(startTasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var stopTasks = new List<Task>();

        foreach (var subscription in _subscriptions.Values)
        {
            var stopTask = subscription.StopAsync(cancellationToken);
            stopTasks.Add(stopTask);
        }

        await Task.WhenAll(stopTasks);
    }

    public void Send(string subscriptionName, object eventData, EventInfo eventInfo)
    {
        _subscriptions[subscriptionName].Send(eventData, eventInfo);
    }
}
