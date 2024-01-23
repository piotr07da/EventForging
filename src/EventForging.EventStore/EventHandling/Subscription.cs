using System.Text.Json;
using EventForging.EventsHandling;
using EventForging.Serialization;
using EventStore.Client;
using Microsoft.Extensions.Logging;

namespace EventForging.EventStore.EventHandling;

internal sealed class Subscription
{
    private readonly SubscriptionConfiguration _subscriptionConfiguration;
    private readonly EventStorePersistentSubscriptionsClient _persistentSubscriptionsClient;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger _logger;

    private PersistentSubscription? _subscription;
    private bool _unsubscribeRequested;

    public Subscription(
        SubscriptionConfiguration subscriptionConfiguration,
        EventStorePersistentSubscriptionsClient persistentSubscriptionsClient,
        IEventDispatcher eventDispatcher,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        ILogger logger)
    {
        _subscriptionConfiguration = subscriptionConfiguration ?? throw new ArgumentNullException(nameof(subscriptionConfiguration));
        _persistentSubscriptionsClient = persistentSubscriptionsClient ?? throw new ArgumentNullException(nameof(persistentSubscriptionsClient));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider ?? throw new ArgumentNullException(nameof(jsonSerializerOptionsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        await CreateOrUpdatePersistentSubscriptionAsync();

#if NETSTANDARD2_0 || NETSTANDARD2_1
        _subscription = await _persistentSubscriptionsClient.SubscribeAsync(_subscriptionConfiguration.StreamId, _subscriptionConfiguration.GroupName, OnEventAppeared, OnSubscriptionDropped, autoAck: false, cancellationToken: cancellationToken);
#else
        _subscription = await _persistentSubscriptionsClient.SubscribeToStreamAsync(_subscriptionConfiguration.StreamId, _subscriptionConfiguration.GroupName, OnEventAppeared, OnSubscriptionDropped, cancellationToken: cancellationToken);
#endif
    }

    public async Task UnsubscribeAsync(CancellationToken cancellationToken)
    {
        _unsubscribeRequested = true;
        _subscription!.Dispose();
        await Task.CompletedTask;
    }

    private async Task CreateOrUpdatePersistentSubscriptionAsync()
    {
        var settings = new PersistentSubscriptionSettings(
            startFrom: _subscriptionConfiguration.StartFrom == null ? null : new StreamPosition(_subscriptionConfiguration.StartFrom.Value),
            resolveLinkTos: true
        );

        var streamId = _subscriptionConfiguration.StreamId;
        var groupName = _subscriptionConfiguration.GroupName;

        try
        {
            await _persistentSubscriptionsClient.CreateAsync(streamId, groupName, settings);
        }
        catch
        {
            await _persistentSubscriptionsClient.UpdateAsync(streamId, groupName, settings);
        }
    }

    private async Task OnEventAppeared(PersistentSubscription subscription, ResolvedEvent re, int? retryCount, CancellationToken cancellationToken)
    {
        try
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse - in reality it can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract - in reality it can be null
            if (re.Event == null)
            {
                await subscription.Nack(PersistentSubscriptionNakEventAction.Park, "Event is null, probably whole stream does not exist anymore.", re);
                return;
            }

            var e = _eventSerializer.DeserializeFromBytes(re.Event.EventType, re.Event.Data.ToArray());
            var emd = JsonSerializer.Deserialize<EventMetadata>(re.Event.Metadata.ToArray(), _jsonSerializerOptionsProvider.Get())!;

            await _eventDispatcher.DispatchAsync(_subscriptionConfiguration.SubscriptionName, e, new EventInfo(re.Event.EventStreamId, re.Event.EventId.ToGuid(), re.Event.EventNumber.ToInt64(), re.Event.EventType, emd.ConversationId, emd.InitiatorId, re.Event.Created, emd.CustomProperties ?? new Dictionary<string, string>()), cancellationToken);

            if (_unsubscribeRequested)
            {
                await subscription.Nack(PersistentSubscriptionNakEventAction.Retry, "Client unsubscribed.", re);
            }

            await subscription.Ack(re);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, exception.Message);
            await subscription.Nack(_subscriptionConfiguration.EventHandlingExceptionNakAction, exception.Message, re);
        }
    }

    private void OnSubscriptionDropped(PersistentSubscription subscription, SubscriptionDroppedReason reason, Exception? exception)
    {
        _logger.LogError(exception, $"Eventstore subscription {_subscriptionConfiguration.SubscriptionName} for [{_subscriptionConfiguration.StreamId}/{_subscriptionConfiguration.GroupName}] dropped. Reason: {reason}.");
#pragma warning disable 4014
        SubscribeAsync(CancellationToken.None);
#pragma warning restore 4014
    }
}
