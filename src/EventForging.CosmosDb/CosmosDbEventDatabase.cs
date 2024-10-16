﻿using System.Diagnostics;
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
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;
    private readonly ILogger _logger;

    public CosmosDbEventDatabase(
        ICosmosDbProvider cosmosDbProvider,
        IStreamIdFactory streamIdFactory,
        IEventForgingConfiguration configuration,
        ICosmosDbEventForgingConfiguration cosmosConfiguration,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider,
        IEventForgingLoggerProvider loggerProvider)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _streamIdFactory = streamIdFactory ?? throw new ArgumentNullException(nameof(streamIdFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cosmosConfiguration = cosmosConfiguration ?? throw new ArgumentNullException(nameof(cosmosConfiguration));
        _eventSerializer = eventSerializer;
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
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
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseReadActivity();

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
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseWriteActivity(retrievedVersion);

        try
        {
            if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));
            if (events == null) throw new ArgumentNullException(nameof(events));

            if (events.Count == 0)
            {
                return;
            }

            var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);

            activity.EnrichEventDatabaseWriteActivityWithStreamId(streamId);

            var originalRetrievedVersion = retrievedVersion;

            var retryCountForUnexpectedVersionWhenExpectedVersionIsAny = _cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny;

            var tryIndex = 0;
            while (tryIndex <= retryCountForUnexpectedVersionWhenExpectedVersionIsAny)
            {
                activity.EnrichEventDatabaseWriteActivityWithTryCount(tryIndex + 1);

                try
                {
                    await InternalWriteAsync<TAggregate>(aggregateId, streamId, events, retrievedVersion, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken);
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

        var pageCount = 0;
        var totalRequestCharge = 0.0;

        var iterator = container.IterateAsync(
            new QueryDefinition("SELECT * FROM x ORDER BY x.eventNumber"),
            new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, },
            _serializerOptionsProvider.Get(),
            pageResponseMessage =>
            {
                ++pageCount;
                totalRequestCharge += pageResponseMessage.Headers.RequestCharge;
                activity.EnrichEventDatabaseReadActivityWithReadPageInformation(pageCount, totalRequestCharge);
                activity.RecordEventDatabaseReadActivityResultPageReadEvent(pageResponseMessage.StatusCode, pageResponseMessage.Headers.RequestCharge);
            },
            cancellationToken);

        await foreach (var item in iterator)
        {
            if (item.TryHandleAs<EventDocument>(DocumentType.Event.ToString(), out var eventDocument))
            {
                var eventId = Guid.Parse(eventDocument.Id!);
                var deserializedEventData = DeserializeEventData(eventDocument.StreamId!, eventDocument.Id!, eventId, eventDocument.Data, eventDocument.EventType);

                yield return new EventDatabaseRecord(
                    eventId,
                    eventDocument.EventNumber,
                    eventDocument.EventType!,
                    DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).DateTime,
                    deserializedEventData,
                    eventDocument.Metadata?.ConversationId ?? Guid.Empty,
                    eventDocument.Metadata?.InitiatorId ?? Guid.Empty,
                    eventDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
            }

            if (item.TryHandleAs<EventsPacketDocument>(DocumentType.EventsPacket.ToString(), out var eventsPacketDocument))
            {
                foreach (var e in eventsPacketDocument.Events ?? throw new EventForgingException($"Events packet {eventsPacketDocument.Id ?? "NULL"} has no events."))
                {
                    var deserializeEventData = DeserializeEventData(eventsPacketDocument.StreamId!, eventsPacketDocument.Id!, e.EventId, e.Data, e.EventType);

                    yield return new EventDatabaseRecord(
                        e.EventId,
                        e.EventNumber,
                        e.EventType!,
                        DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).DateTime,
                        deserializeEventData,
                        eventsPacketDocument.Metadata?.ConversationId ?? Guid.Empty,
                        eventsPacketDocument.Metadata?.InitiatorId ?? Guid.Empty,
                        eventsPacketDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
                }
            }
        }
    }

    private object DeserializeEventData(string streamId, string documentId, Guid eventId, object? serializedEventData, string eventType)
    {
        if (serializedEventData is null)
        {
            throw new EventForgingException($"Event data retrieved from the database cannot be null. Stream Id is '{streamId}', Document Id is {documentId}, Event Id is {eventId}.");
        }

        var eventDataAsString = serializedEventData.ToString()!;
        var eventData = _eventSerializer.DeserializeFromString(eventType, eventDataAsString);
        return eventData;
    }

    private async Task InternalWriteAsync<TAggregate>(string aggregateId, string streamId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseWriteAttemptActivity(retrievedVersion);

        customProperties.StoreCurrentActivityId();

        try
        {
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
                                var epdEvent = CreateStreamEventsPacketDocumentEvent(initiatorId, retrievedVersion, eIx, e);
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
                    // "Cosmos DB transactions support a maximum of 100 operations. One operation is reserved for stream metadata write. As a result, a maximum of 99 events can be saved."
                    throw new EventForgingException($"Max number of events is {MaxNumberOfUnpackedEventsInTransaction}.");
                }
            }
            else if (_cosmosConfiguration.EventPacking is EventPackingMode.AllEventsInOnePacket)
            {
                var epdEvents = new List<EventsPacketDocument.Event>();
                for (var eIx = 0; eIx < events.Count; ++eIx)
                {
                    var epdEvent = CreateStreamEventsPacketDocumentEvent(initiatorId, retrievedVersion, eIx, events[eIx]);
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
                var alreadyWritten = await CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(streamId, initiatorId, activity, cancellationToken);
                if (alreadyWritten)
                {
                    _logger.WriteIgnoredDueToIdempotencyCheck(streamId, initiatorId);
                    return;
                }

                var actualVersion = await ReadCurrentVersionAsync<TAggregate>(streamId, activity, cancellationToken);

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

    private async Task<int> ReadCurrentVersionAsync<TAggregate>(string streamId, Activity? activity, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetContainer<TAggregate>().ReadItemAsync<HeaderDocument>(HeaderDocument.CreateId(streamId), new PartitionKey(streamId), cancellationToken: cancellationToken);

            var currentVersion = result.Resource.Version;

            activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("Current version of the aggregate has been read.", result.StatusCode, result.RequestCharge, new Dictionary<string, string> { { TracingAttributeNames.AggregateVersion, currentVersion.ToString() }, });

            return currentVersion;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("An exception occurred during the read of the current version of the aggregate.", ex.StatusCode, ex.RequestCharge);

            throw new EventForgingStreamNotFoundException(streamId, ex);
        }
    }

    private async Task<bool> CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(string streamId, Guid initiatorId, Activity? activity, CancellationToken cancellationToken = default)
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

        activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent(
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

    private EventsPacketDocument.Event CreateStreamEventsPacketDocumentEvent(Guid initiatorId, AggregateVersion retrievedVersion, int eventIndex, object eventData)
    {
        var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eventIndex) : Guid.NewGuid();
        var eventDataAsJsonElement = _eventSerializer.SerializeToJsonElement(eventData, out var eventName);
        var eventsPacketEvent = new EventsPacketDocument.Event(
            eventId,
            retrievedVersion + eventIndex + 1L,
            eventName,
            eventDataAsJsonElement);
        return eventsPacketEvent;
    }

    private EventsPacketDocument CreateStreamEventsPacketDocument(string streamId, IReadOnlyList<EventsPacketDocument.Event> events, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
    {
        return new EventsPacketDocument(streamId, events, new EventMetadata(conversationId, initiatorId, customProperties));
    }

    private EventDocument CreateStreamEventDocument(string streamId, Guid eventId, long eventNumber, object eventData, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
    {
        var eventDataAsJsonElement = _eventSerializer.SerializeToJsonElement(eventData, out var eventName);
        return new EventDocument(streamId, eventId, eventNumber, eventDataAsJsonElement, eventName, new EventMetadata(conversationId, initiatorId, customProperties));
    }

    private static HeaderDocument CreateStreamHeaderDocument(string streamId, int eventsCount)
    {
        var header = new HeaderDocument(streamId);
        header.Version += eventsCount;
        return header;
    }
}
