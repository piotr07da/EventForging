using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using EventForging.CosmosDb.Diagnostics.Logging;
using EventForging.CosmosDb.Diagnostics.Tracing;
using EventForging.Diagnostics.Logging;
using EventForging.Diagnostics.Tracing;
using EventForging.EnumerationExtensions;
using EventForging.Idempotency;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventDatabase : IEventDatabase
{
    private const int MaxNumberOfUnpackedEventsInTransaction = 99;

    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IStreamIdFactory _streamIdFactory;
    private readonly IEventForgingConfiguration _configuration;
    private readonly ICosmosDbEventForgingConfiguration _cosmosConfiguration;
    private readonly ILogger _logger;

    public CosmosDbEventDatabase(
        ICosmosDbProvider cosmosDbProvider,
        IStreamIdFactory streamIdFactory,
        IEventForgingConfiguration configuration,
        ICosmosDbEventForgingConfiguration cosmosConfiguration,
        IEventForgingLoggerProvider loggerProvider)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _streamIdFactory = streamIdFactory ?? throw new ArgumentNullException(nameof(streamIdFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cosmosConfiguration = cosmosConfiguration ?? throw new ArgumentNullException(nameof(cosmosConfiguration));
        _logger = loggerProvider.Logger;
    }

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsAsync<TAggregate>(aggregateId, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
    {
        var activity = EventForgingActivitySourceProvider.ActivitySource.StartEventDatabaseReadActivity();

        try
        {
            var records = InternalReadRecordsAsync<TAggregate>(aggregateId, activity, cancellationToken);

            return records.WithExceptionIntercept(
                ex =>
                {
                    activity?.RecordException(ex);
                },
                () =>
                {
                    activity?.Complete();
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.Complete();
            throw;
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var activity = EventForgingActivitySourceProvider.ActivitySource.StartEventDatabaseWriteActivity(retrievedVersion);

        try
        {
            var originalRetrievedVersion = retrievedVersion;

            var retryCountForUnexpectedVersionWhenExpectedVersionIsAny = _cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny;

            var tryIndex = 0;
            while (tryIndex <= retryCountForUnexpectedVersionWhenExpectedVersionIsAny)
            {
                activity.EnrichEventDatabaseWriteActivityWithTryCount(tryIndex + 1);

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

                    if (tryIndex == retryCountForUnexpectedVersionWhenExpectedVersionIsAny || ex.ActualVersion is null)
                    {
                        throw new EventForgingUnexpectedVersionException(ex.AggregateId, ex.StreamId, ex.ExpectedVersion, originalRetrievedVersion, ex.ActualVersion, ex);
                    }

                    ++tryIndex;

                    _logger.RetryingDueToUnexpectedVersionOfAggregateWhenExpectedVersionIsAny(ex, aggregateId, retrievedVersion, ex.ActualVersion.Value, tryIndex, retryCountForUnexpectedVersionWhenExpectedVersionIsAny);

                    retrievedVersion = ex.ActualVersion.Value;
                }
            }
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

    private async IAsyncEnumerable<EventDatabaseRecord> InternalReadRecordsAsync<TAggregate>(string aggregateId, Activity? activity, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));

        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);

        activity.EnrichEventDatabaseReadActivityWithStreamId(streamId);

        var container = GetContainer<TAggregate>();

        var iterator = container.GetItemQueryIterator<MasterDocument>("SELECT * FROM x ORDER BY x.eventNumber", requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        var pageCount = 0;
        var totalRequestCharge = 0.0;

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            ++pageCount;
            totalRequestCharge += page.RequestCharge;
            activity.EnrichEventDatabaseReadActivityWithReadPageInformation(pageCount, totalRequestCharge);
            activity.RecordEventDatabaseReadActivityResultPageReadEvent(page.StatusCode, page.RequestCharge);

            foreach (var masterDocument in page)
            {
                switch (masterDocument.DocumentType)
                {
                    case DocumentType.Undefined:
                    case DocumentType.Header:
                    default:
                        continue;

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

    private async Task InternalWriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var activity = EventForgingActivitySourceProvider.ActivitySource.StartEventDatabaseWriteAttemptActivity(retrievedVersion);

        customProperties.StoreCurrentActivityId();

        try
        {
            if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));
            if (events == null) throw new ArgumentNullException(nameof(events));

            if (events.Count == 0)
            {
                return;
            }

            var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);

            activity?.EnrichEventDatabaseWriteAttemptActivityWithStreamId(streamId);

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

            if (_cosmosConfiguration.EventPacking is EventPackingMode.Disabled or EventPackingMode.UniformDistributionFilling)
            {
                if (events.Count <= MaxNumberOfUnpackedEventsInTransaction)
                {
                    for (var eIx = 0; eIx < events.Count; ++eIx)
                    {
                        var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();
                        var eventDocument = CreateStreamEventDocument(streamId, eventId, retrievedVersion + eIx + 1L, events[eIx], conversationId, initiatorId, customProperties);
                        transaction.CreateItem(eventDocument, requestOptions);
                    }
                }
                else if (_cosmosConfiguration.EventPacking is EventPackingMode.UniformDistributionFilling)
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

                            var eventsPacketDocument = CreateStreamEventsPacketDocument(streamId, epdEvents, conversationId, initiatorId, customProperties);

                            transaction.CreateItem(eventsPacketDocument, requestOptions);
                        }
                    }
                }
                else
                {
                    // https://docs.microsoft.com/en-us/azure/cosmos-db/sql/transactional-batch
                    // "Cosmos DB transactions support a maximum of 100 operations. One operation is reserved for stream metadata write. As a result, a maximum of 99 events can be saved.
                    throw new EventForgingException($"Max number of events is {MaxNumberOfUnpackedEventsInTransaction}.");
                }
            }
            else if (_cosmosConfiguration.EventPacking is EventPackingMode.AllEventsInOnePacket)
            {
                var epdEvents = new List<EventsPacketDocument.Event>();
                for (var eIx = 0; eIx < events.Count; ++eIx)
                {
                    var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();

                    var epdEvent = new EventsPacketDocument.Event
                    {
                        EventId = eventId,
                        EventNumber = retrievedVersion + eIx + 1L,
                        Data = events[eIx],
                    };

                    epdEvents.Add(epdEvent);
                }

                var eventsPacketDocument = CreateStreamEventsPacketDocument(streamId, epdEvents, conversationId, initiatorId, customProperties);

                transaction.CreateItem(eventsPacketDocument, requestOptions);
            }
            else
            {
                throw new EventForgingException($"Unknown event packing mode: {_cosmosConfiguration.EventPacking}.");
            }

            var response = await transaction.ExecuteAsync(cancellationToken);

            activity?.EnrichEventDatabaseWriteAttemptActivityWithResponse(response);

            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {
                var alreadyWritten = await CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(streamId, initiatorId, cancellationToken);
                if (alreadyWritten)
                {
                    _logger.WriteIgnoredDueToIdempotencyCheck(streamId, initiatorId);
                    return;
                }

                var actualVersion = await ReadCurrentVersionAsync<TAggregate>(streamId, cancellationToken);

                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion);
            }

            if (!response.IsSuccessStatusCode)
                throw new EventForgingException(response.ErrorMessage);
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

    private async Task<int> ReadCurrentVersionAsync<TAggregate>(string streamId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetContainer<TAggregate>().ReadItemAsync<MasterDocument>(HeaderDocument.CreateId(streamId), new PartitionKey(streamId), cancellationToken: cancellationToken);

            var currentVersion = result.Resource.HeaderDocument!.Version;

            Activity.Current.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("Current version of the aggregate has been read.", result.StatusCode, result.RequestCharge, new Dictionary<string, string> { { TracingAttributeNames.AggregateVersion, currentVersion.ToString() }, });

            return currentVersion;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Activity.Current.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("An exception occurred during the read of the current version of the aggregate.", ex.StatusCode, ex.RequestCharge);

            throw new EventForgingStreamNotFoundException(streamId, ex);
        }
    }

    private async Task<bool> CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(string streamId, Guid initiatorId, CancellationToken cancellationToken = default)
    {
        if (initiatorId == Guid.Empty)
            return false;

        var query = new QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE c.metadata.initiatorId = @initiatorId AND (c.documentType = '{DocumentType.Event.ToString()}' OR c.documentType = '{DocumentType.EventsPacket.ToString()}')")
            .WithParameter("@initiatorId", initiatorId.ToString());
        var iterator = GetContainer<TAggregate>().GetItemQueryIterator<int>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        var page = await iterator.ReadNextAsync(cancellationToken);

        if (page is null)
        {
            return false;
        }

        var first = page.FirstOrDefault();
        var checkResult = first > 0;

        Activity.Current.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent(
            "The check for any events associated with the given initiatorId has been successfully completed.",
            page.StatusCode,
            page.RequestCharge,
            new Dictionary<string, string>
            {
                { TracingAttributeNames.InitiatorId, initiatorId.ToString() },
                { CosmosDbTracingAttributeNames.EventDatabaseWriteIdempotencyCheckResult, checkResult.ToString().ToLower() },
            });

        return checkResult;
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
}
