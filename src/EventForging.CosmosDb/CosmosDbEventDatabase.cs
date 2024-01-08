using System.Net;
using System.Runtime.CompilerServices;
using EventForging.Idempotency;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventDatabase : IEventDatabase
{
    public const int MaxNumberOfUnpackedEventsInTransaction = 99;

    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventForgingConfiguration _configuration;
    private readonly ICosmosDbEventForgingConfiguration _cosmosConfiguration;
    private readonly ILogger _logger;

    public CosmosDbEventDatabase(
        ICosmosDbProvider cosmosDbProvider,
        IStreamNameFactory streamNameFactory,
        IEventForgingConfiguration configuration,
        ICosmosDbEventForgingConfiguration cosmosConfiguration,
        ILoggerFactory? loggerFactory = null)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cosmosConfiguration = cosmosConfiguration ?? throw new ArgumentNullException(nameof(cosmosConfiguration));
        _logger = loggerFactory.CreateEventForgingLogger();
    }

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsAsync<TAggregate>(aggregateId, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var container = GetContainer<TAggregate>();

        var iterator = container.GetItemQueryIterator<MasterDocument>($"SELECT * FROM x WHERE x.documentType = '{nameof(DocumentType.Event)}' OR x.documentType = '{nameof(DocumentType.EventsPacket)}' ORDER BY x.eventNumber", requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var masterDocument in page)
            {
                switch (masterDocument.DocumentType)
                {
                    case DocumentType.Event:
                        var eventDocument = masterDocument.EventDocument!;
                        if (eventDocument.Data is null)
                            throw new EventForgingException($"Event {eventDocument.Id ?? "NULL"} has no data.");
                        yield return new EventDatabaseRecord(
                            Guid.Parse(eventDocument.Id!),
                            eventDocument.EventNumber,
                            eventDocument.EventType!,
                            DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).DateTime,
                            eventDocument.Data,
                            eventDocument.Metadata?.ConversationId ?? Guid.Empty,
                            eventDocument.Metadata?.InitiatorId ?? Guid.Empty,
                            eventDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
                        break;

                    case DocumentType.EventsPacket:
                        var eventsPacketDocument = masterDocument.EventsPacketDocument!;
                        foreach (var e in eventsPacketDocument.Events ?? throw new EventForgingException($"Events packet {eventsPacketDocument.Id ?? "NULL"} has no events."))
                        {
                            if (e.Data is null)
                                throw new EventForgingException($"Event {e.EventId} has no data.");

                            yield return new EventDatabaseRecord(
                                e.EventId,
                                e.EventNumber,
                                e.EventType!,
                                DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).DateTime,
                                e.Data,
                                eventsPacketDocument.Metadata?.ConversationId ?? Guid.Empty,
                                eventsPacketDocument.Metadata?.InitiatorId ?? Guid.Empty,
                                eventsPacketDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
                        }

                        break;
                }
            }
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var originalRetrievedVersion = retrievedVersion;

        var tryIndex = 0;
        while (tryIndex <= _cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny)
        {
            try
            {
                await InternalWriteAsync<TAggregate>(aggregateId, events, retrievedVersion, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken);
                return;
            }
            catch (EventForgingUnexpectedVersionException ex)
            {
                if (expectedVersion != ExpectedVersion.Any)
                {
                    throw;
                }

                if (tryIndex == _cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny || ex.ActualVersion is null)
                {
                    throw new EventForgingUnexpectedVersionException(ex.AggregateId, ex.StreamId, ex.ExpectedVersion, originalRetrievedVersion, ex.ActualVersion, ex);
                }

                ++tryIndex;
                retrievedVersion = ex.ActualVersion.Value;

                _logger.LogDebug(ex, $"Unexpected version of aggregate {aggregateId} detected. Retrying ({tryIndex}/{_cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny}).");
            }
        }
    }

    public async Task InternalWriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));
        if (events == null) throw new ArgumentNullException(nameof(events));

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var requestOptions = new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false, };
        var transaction = GetContainer<TAggregate>().CreateTransactionalBatch(new PartitionKey(streamId));

        if (retrievedVersion.AggregateDoesNotExist)
            transaction.CreateItem(CreateStreamHeaderDocument(streamId, events.Count), requestOptions);
        else
        {
            long expectedHeaderVersion;
            if (expectedVersion.IsAny || expectedVersion.IsRetrieved)
            {
                // IsAny is treated the same as IsRetrieved because event numbers are numbered using retrieved version. There is no way, at least as of 05/06/2023,
                // to read version from header document and use read value in the same transaction.
                expectedHeaderVersion = retrievedVersion;
            }
            else if (expectedVersion.IsNone)
            {
                // Because this is the case in which lastReadAggregateVersion.AggregateExists is true then this case (expectedVersion.IsNone) will never occur
                // due to the check performed in the Repository class (lastReadAggregateVersion.AggregateExists && expectedVersion.IsNone already throws exception).
                // I left this code for clarity.
                expectedHeaderVersion = -1L;
            }
            else
                expectedHeaderVersion = expectedVersion;

            var headerPatchRequestOptions = new TransactionalBatchPatchItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
                FilterPredicate = $"FROM x WHERE x.version = {expectedHeaderVersion}",
            };

            transaction.PatchItem(HeaderDocument.CreateId(streamId), new[] { PatchOperation.Increment("/version", events.Count), }, headerPatchRequestOptions);
        }

        if (events.Count <= MaxNumberOfUnpackedEventsInTransaction)
        {
            for (var eIx = 0; eIx < events.Count; ++eIx)
            {
                var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();
                var eventDocument = CreateStreamEventDocument(streamId, eventId, retrievedVersion + eIx + 1L, events[eIx], conversationId, initiatorId, customProperties);
                transaction.CreateItem(eventDocument, requestOptions);
            }
        }
        else if (_cosmosConfiguration.EnableEventPacking)
        {
            var eventPackets = events.SplitEvenly(MaxNumberOfUnpackedEventsInTransaction);
            var eIx = 0;
            foreach (var ep in eventPackets)
            {
                if (ep.Count == 1)
                {
                    var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();
                    var eventDocument = CreateStreamEventDocument(streamId, eventId, retrievedVersion + eIx + 1L, ep[0], conversationId, initiatorId, customProperties);
                    transaction.CreateItem(eventDocument, requestOptions);
                    ++eIx;
                }
                else
                {
                    var epdEvents = new List<EventsPacketDocument.Event>();
                    foreach (var e in ep)
                    {
                        var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();

                        var epdEvent = new EventsPacketDocument.Event
                        {
                            EventId = eventId,
                            EventNumber = retrievedVersion + eIx + 1L,
                            Data = e,
                        };

                        epdEvents.Add(epdEvent);
                        ++eIx;
                    }

                    var eventsPackageDocument = CreateStreamEventsPacketDocument(streamId, epdEvents, conversationId, initiatorId, customProperties);

                    transaction.CreateItem(eventsPackageDocument, requestOptions);
                }
            }
        }
        else
        {
            // https://docs.microsoft.com/en-us/azure/cosmos-db/sql/transactional-batch
            // "Cosmos DB transactions support a maximum of 100 operations. One operation is reserved for stream metadata write. As a result, a maximum of 99 events can be saved.
            throw new EventForgingException($"Max number of events is {MaxNumberOfUnpackedEventsInTransaction}.");
        }

        var response = await transaction.ExecuteAsync(cancellationToken);

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            var alreadyWritten = await CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(aggregateId, initiatorId, cancellationToken);
            if (alreadyWritten)
            {
                LogIdempotencyCheck(aggregateId, initiatorId);
                return;
            }

            var headerItem = await ReadHeaderAsync<TAggregate>(streamId, cancellationToken);

            throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, retrievedVersion, headerItem.Version);
        }

        if (!response.IsSuccessStatusCode)
            throw new EventForgingException(response.ErrorMessage);
    }

    private async Task<HeaderDocument> ReadHeaderAsync<TAggregate>(string streamId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetContainer<TAggregate>().ReadItemAsync<MasterDocument>(HeaderDocument.CreateId(streamId), new PartitionKey(streamId), cancellationToken: cancellationToken);
            return result.Resource.HeaderDocument!;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new EventForgingStreamNotFoundException(streamId, ex);
        }
    }

    private async Task<bool> CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(string aggregateId, Guid initiatorId, CancellationToken cancellationToken = default)
    {
        if (initiatorId == Guid.Empty)
            return false;

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var query = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE c.metadata.initiatorId = @initiatorId AND (c.documentType = '{DocumentType.Event.ToString()}' OR c.documentType = '{DocumentType.EventsPacket.ToString()}')")
            .WithParameter("@initiatorId", initiatorId.ToString());
        var iterator = GetContainer<TAggregate>().GetItemQueryIterator<int>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        var page = await iterator.ReadNextAsync(cancellationToken);
        var first = page.First();

        return first > 0;
    }

    private Container GetContainer<TAggregate>()
    {
        return _cosmosDbProvider.GetAggregateContainer<TAggregate>();
    }

    private static EventsPacketDocument CreateStreamEventsPacketDocument(string streamId, IReadOnlyList<EventsPacketDocument.Event> events, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
    {
        return new EventsPacketDocument(streamId, events, new EventMetadata(conversationId, initiatorId, customProperties));
    }

    private static EventDocument CreateStreamEventDocument(string streamId, Guid eventId, long eventNumber, object eventData, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
    {
        return new EventDocument(streamId, eventId, eventNumber, eventData, new EventMetadata(conversationId, initiatorId, customProperties));
    }

    private static HeaderDocument CreateStreamHeaderDocument(string streamId, int eventsCount)
    {
        var header = new HeaderDocument(streamId);
        header.Version += eventsCount;
        return header;
    }

    private void LogIdempotencyCheck(string aggregateId, Guid initiatorId)
    {
        _logger.LogDebug($"Cannot write events for aggregate {aggregateId} because these events has already been written for the same initiatorId '{initiatorId}'.");
    }
}
