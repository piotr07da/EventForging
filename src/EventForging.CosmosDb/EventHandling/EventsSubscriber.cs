using EventForging.CosmosDb.Serialization;
using EventForging.Diagnostics.Logging;
using EventForging.EventsHandling;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb.EventHandling;

internal sealed class EventsSubscriber : IEventsSubscriber
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ICosmosDbEventForgingConfiguration _configuration;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;
    private readonly ILogger _logger;

    private readonly IList<ChangeFeedProcessor> _changeFeedProcessors = new List<ChangeFeedProcessor>();

    // ReSharper disable once ConvertToPrimaryConstructor
    public EventsSubscriber(
        ICosmosDbProvider cosmosDbProvider,
        IEventDispatcher eventDispatcher,
        ICosmosDbEventForgingConfiguration configuration,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider,
        IEventForgingLoggerProvider loggerProvider)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
        _logger = loggerProvider.Logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _configuration.Subscriptions)
        {
            var container = _cosmosDbProvider.GetContainer(subscription.DatabaseName, subscription.EventsContainerName);

            var changeFeedProcessorBuilder = container
                .GetChangeFeedProcessorBuilder(
                    subscription.ChangeFeedName,
                    (context, changes, ct) => HandleChangesAsync(subscription.SubscriptionName, context, changes, ct))
                .WithInstanceName(Environment.MachineName)
                .WithLeaseContainer(_cosmosDbProvider.GetLeaseContainer(subscription.DatabaseName));

            if (subscription.StartTime.HasValue)
            {
                changeFeedProcessorBuilder = changeFeedProcessorBuilder.WithStartTime(subscription.StartTime.Value);
            }

            if (subscription.PollInterval.HasValue)
            {
                changeFeedProcessorBuilder = changeFeedProcessorBuilder.WithPollInterval(subscription.PollInterval.Value);
            }

            var changeFeedProcessor = changeFeedProcessorBuilder.Build();

            await changeFeedProcessor.StartAsync();
            _changeFeedProcessors.Add(changeFeedProcessor);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var changeFeedProcessor in _changeFeedProcessors)
        {
            try
            {
                await changeFeedProcessor.StopAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred during the stopping operation of the change feed processor.");
            }
        }
    }

    private async Task HandleChangesAsync(string subscriptionName, ChangeFeedProcessorContext context, Stream changes, CancellationToken cancellationToken)
    {
        var batchesByStreamId = new Dictionary<string, List<ReceivedEvent>>();
        var streamIdsInOrder = new List<string>();

        await foreach (var containerItem in changes.DeserializeStreamAsync(_serializerOptionsProvider.Get(), cancellationToken))
        {
            var streamId = containerItem.GetStringValue("streamId");
            if (!batchesByStreamId.TryGetValue(streamId, out var batch))
            {
                batch = new List<ReceivedEvent>();
                batchesByStreamId.Add(streamId, batch);
                streamIdsInOrder.Add(streamId);
            }

            foreach (var receivedEvent in ExtractReceivedEvents(containerItem))
            {
                batch.Add(receivedEvent);
            }
        }

        foreach (var streamId in streamIdsInOrder)
        {
            var batch = batchesByStreamId[streamId];
            if (batch.Count == 0)
            {
                continue;
            }

            var receivedEventsBatch = new ReceivedEventsBatch(batch);
            await _eventDispatcher.DispatchAsync(subscriptionName, receivedEventsBatch, cancellationToken);
        }
    }

    private IEnumerable<ReceivedEvent> ExtractReceivedEvents(ContainerItem containerItem)
    {
        if (containerItem.TryHandleAs<EventDocument>(nameof(DocumentType.Event), out var eventDocument))
        {
            if (eventDocument.IsDeleted == true)
            {
                yield break;
            }

            var md = eventDocument.Metadata;
            var ei = new EventInfo(eventDocument.StreamId!, Guid.Parse(eventDocument.Id!), eventDocument.EventNumber, eventDocument.EventType, md!.ConversationId, md.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).UtcDateTime, md.CustomProperties ?? new Dictionary<string, string>());
            var deserializedEventData = _eventSerializer.DeserializeFromString(eventDocument.EventType, eventDocument.Data!.ToString()!);
            yield return new ReceivedEvent(deserializedEventData, ei);
        }
        else if (containerItem.TryHandleAs<EventsPacketDocument>(nameof(DocumentType.EventsPacket), out var eventsPacketDocument))
        {
            if (eventsPacketDocument.IsDeleted == true)
            {
                yield break;
            }

            var md = eventsPacketDocument.Metadata;

            foreach (var e in eventsPacketDocument.Events)
            {
                var ei = new EventInfo(eventsPacketDocument.StreamId!, e.EventId, e.EventNumber, e.EventType, md!.ConversationId, md.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).UtcDateTime, md.CustomProperties ?? new Dictionary<string, string>());
                var deserializedEventData = _eventSerializer.DeserializeFromString(e.EventType, e.Data!.ToString()!);
                yield return new ReceivedEvent(deserializedEventData, ei);
            }
        }
    }
}
