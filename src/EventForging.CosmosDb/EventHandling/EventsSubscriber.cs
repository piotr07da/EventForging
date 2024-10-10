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

    private readonly GroupsMerger<ContainerItem, ReceivedEvent> _changesMerger;

    private readonly IList<ChangeFeedProcessor> _changeFeedProcessors = new List<ChangeFeedProcessor>();
    private bool _stopRequested;

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

        _changesMerger = CreateChangesMerger();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _configuration.Subscriptions)
        {
            var container = _cosmosDbProvider.GetContainer(subscription.DatabaseName, subscription.EventsContainerName);

            var changeFeedProcessorBuilder = container
                .GetChangeFeedProcessorBuilder(
                    subscription.ChangeFeedName,
                    (context, changes, cancellationToken) => HandleChangesAsync(subscription.SubscriptionName, context, changes, cancellationToken))
                .WithInstanceName(Environment.MachineName)
                .WithLeaseContainer(_cosmosDbProvider.GetLeaseContainer(subscription.DatabaseName));

            if (subscription.StartTime.HasValue)
            {
                changeFeedProcessorBuilder = changeFeedProcessorBuilder.WithStartTime(subscription.StartTime.Value);
            }

            var changeFeedProcessor = changeFeedProcessorBuilder.Build();

            await changeFeedProcessor.StartAsync();
            _changeFeedProcessors.Add(changeFeedProcessor);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;

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
        const string stopRequestedExceptionMessage = "Stop has been requested. Cannot finish processing for received changes. Batch of changes will not be confirmed and will be redelivered.";

        var containerItems = new List<ContainerItem>();
        await foreach (var containerItem in changes.DeserializeStreamAsync(_serializerOptionsProvider.Get(), cancellationToken))
        {
            containerItems.Add(containerItem);
        }

        var batches = _changesMerger.Merge(containerItems);
        foreach (var batch in batches)
        {
            if (batch.Count == 0)
            {
                continue;
            }

            var receivedEventsBatch = new ReceivedEventsBatch(batch);
            await _eventDispatcher.DispatchAsync(subscriptionName, receivedEventsBatch, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Throw after the loop to avoid situation of incorrectly handled changes which is probable after the stop request.
        if (_stopRequested)
        {
            throw new EventForgingException(stopRequestedExceptionMessage);
        }
    }

    private GroupsMerger<ContainerItem, ReceivedEvent> CreateChangesMerger()
    {
        return new GroupsMerger<ContainerItem, ReceivedEvent>(ci => ci.GetStringValue("streamId"), ExtractReceivedEvents);
    }

    private IEnumerable<ReceivedEvent> ExtractReceivedEvents(ContainerItem containerItem)
    {
        if (containerItem.TryHandleAs<EventDocument>(DocumentType.Event.ToString(), out var eventDocument))
        {
            var md = eventDocument.Metadata;
            var ei = new EventInfo(eventDocument.StreamId!, Guid.Parse(eventDocument.Id!), eventDocument.EventNumber, eventDocument.EventType, md!.ConversationId, md.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).DateTime, md.CustomProperties ?? new Dictionary<string, string>());
            var deserializedEventData = _eventSerializer.DeserializeFromString(eventDocument.EventType, eventDocument.Data!.ToString()!);
            yield return new ReceivedEvent(deserializedEventData, ei);
        }
        else if (containerItem.TryHandleAs<EventsPacketDocument>(DocumentType.EventsPacket.ToString(), out var eventsPacketDocument))
        {
            var md = eventsPacketDocument.Metadata;

            foreach (var e in eventsPacketDocument.Events)
            {
                var ei = new EventInfo(eventsPacketDocument.StreamId!, e.EventId, e.EventNumber, e.EventType, md!.ConversationId, md.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).DateTime, md.CustomProperties ?? new Dictionary<string, string>());
                var deserializedEventData = _eventSerializer.DeserializeFromString(e.EventType, e.Data!.ToString()!);
                yield return new ReceivedEvent(deserializedEventData, ei);
            }
        }
    }
}
