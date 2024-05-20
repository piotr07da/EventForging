using EventForging.CosmosDb.Diagnostics.Tracing;
using EventForging.Diagnostics.Logging;
using EventForging.Diagnostics.Tracing;
using EventForging.EventsHandling;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb.EventHandling;

internal sealed class EventsSubscriber : IEventsSubscriber
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ICosmosDbEventForgingConfiguration _configuration;
    private readonly ILogger _logger;

    private readonly GroupsMerger<MasterDocument, ReceivedEvent> _changesMerger = CreateChangesMerger();

    private readonly IList<ChangeFeedProcessor> _changeFeedProcessors = new List<ChangeFeedProcessor>();
    private bool _stopRequested;

    public EventsSubscriber(
        ICosmosDbProvider cosmosDbProvider,
        IEventDispatcher eventDispatcher,
        ICosmosDbEventForgingConfiguration configuration,
        IEventForgingLoggerProvider loggerProvider)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = loggerProvider.Logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _configuration.Subscriptions)
        {
            var container = _cosmosDbProvider.GetContainer(subscription.DatabaseName, subscription.EventsContainerName);

            var changeFeedProcessorBuilder = container
                .GetChangeFeedProcessorBuilder<MasterDocument>(
                    subscription.ChangeFeedName,
                    (_, changes, ct) => HandleChangesAsync(subscription.SubscriptionName, changes, ct))
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

    private async Task HandleChangesAsync(string subscriptionName, IReadOnlyCollection<MasterDocument> changes, CancellationToken cancellationToken)
    {
        const string stopRequestedExceptionMessage = "Stop has been requested. Cannot finish processing for received changes. Batch of changes will not be confirmed and will be redelivered.";

        var batches = _changesMerger.Merge(changes);
        foreach (var batch in batches)
        {
            if (batch.Count == 0)
            {
                continue;
            }

            var receivedEventsBatch = new ReceivedEventsBatch(batch);

            var activity = EventForgingActivitySourceProvider.ActivitySource.StartEventsSubscriberHandleChangesActivity(subscriptionName, receivedEventsBatch);
            try
            {
                await _eventDispatcher.DispatchAsync(subscriptionName, receivedEventsBatch, cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                throw;
            }
            finally
            {
                activity?.Complete();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Throw after the loop to avoid situation of incorrectly handled changes which is probable after the stop request.
        if (_stopRequested)
        {
            throw new EventForgingException(stopRequestedExceptionMessage);
        }
    }

    private static GroupsMerger<MasterDocument, ReceivedEvent> CreateChangesMerger()
    {
        return new GroupsMerger<MasterDocument, ReceivedEvent>(md => md.StreamId, ExtractReceivedEvents);
    }

    private static IEnumerable<ReceivedEvent> ExtractReceivedEvents(MasterDocument masterDocument)
    {
        if (masterDocument.DocumentType == DocumentType.Event)
        {
            var eventDocument = masterDocument.EventDocument!;
            var md = eventDocument.Metadata;
            var ei = new EventInfo(masterDocument.StreamId, Guid.Parse(eventDocument.Id!), eventDocument.EventNumber, eventDocument.EventType!, md!.ConversationId, md!.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).DateTime, md.CustomProperties ?? new Dictionary<string, string>());
            yield return new ReceivedEvent(eventDocument.Data!, ei);
        }
        else if (masterDocument.DocumentType == DocumentType.EventsPacket)
        {
            var eventsPacketDocument = masterDocument.EventsPacketDocument!;
            var md = eventsPacketDocument.Metadata;

            foreach (var e in eventsPacketDocument.Events)
            {
                var ei = new EventInfo(masterDocument.StreamId, e.EventId, e.EventNumber, e.EventType!, md!.ConversationId, md!.InitiatorId, DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).DateTime, md.CustomProperties ?? new Dictionary<string, string>());
                yield return new ReceivedEvent(e.Data!, ei);
            }
        }
    }
}
