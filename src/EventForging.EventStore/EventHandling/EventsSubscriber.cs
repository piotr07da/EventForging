using EventForging.EventsHandling;
using EventForging.Serialization;
using EventStore.Client;
using Microsoft.Extensions.Logging;

namespace EventForging.EventStore.EventHandling;

internal sealed class EventsSubscriber : IEventsSubscriber
{
    private readonly EventStorePersistentSubscriptionsClient _persistentSubscriptionsClient;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly IEventStoreEventForgingConfiguration _configuration;
    private readonly ILogger _logger;

    private readonly IList<Subscription> _subscriptions = new List<Subscription>();

    public EventsSubscriber(
        EventStorePersistentSubscriptionsClient persistentSubscriptionsClient,
        IEventDispatcher eventDispatcher,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        IEventStoreEventForgingConfiguration configuration,
        ILoggerFactory? loggerFactory = null)
    {
        _persistentSubscriptionsClient = persistentSubscriptionsClient ?? throw new ArgumentNullException(nameof(persistentSubscriptionsClient));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider ?? throw new ArgumentNullException(nameof(jsonSerializerOptionsProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = loggerFactory.CreateEventForgingLogger();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscribeTasks = new List<Task>();

        foreach (var subscriptionConfiguration in _configuration.Subscriptions)
        {
            var subscription = new Subscription(subscriptionConfiguration, _persistentSubscriptionsClient, _eventDispatcher, _eventSerializer, _jsonSerializerOptionsProvider, _logger);
            _subscriptions.Add(subscription);

            var subscribeTask = subscription.SubscribeAsync(cancellationToken);
            subscribeTasks.Add(subscribeTask);
        }

        await Task.WhenAll(subscribeTasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var unsubscribeTasks = new List<Task>();
        foreach (var persistentSubscription in _subscriptions)
        {
            var unsubscribeTask = persistentSubscription.UnsubscribeAsync(cancellationToken);
            unsubscribeTasks.Add(unsubscribeTask);
        }

        await Task.WhenAll(unsubscribeTasks);
    }
}
