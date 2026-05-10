using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using EventForging.CosmosDb.Diagnostics.Logging;
using EventForging.CosmosDb.Diagnostics.Metrics;
using EventForging.CosmosDb.Diagnostics.Tracing;
using EventForging.Diagnostics.Logging;
using EventForging.Diagnostics.Tracing;
using EventForging.EnumerationExtensions;
using EventForging.Idempotency;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventDatabase : IEventDatabase, IDestructiveEventDatabase
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

    private IReadOnlyCollection<string> EventDatabaseOperationRequestChargeMetricCustomPropertyTagNames => ((CosmosDbEventForgingConfiguration)_cosmosConfiguration).EventDatabaseOperationRequestChargeMetricCustomPropertyTagNames;

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseReadActivity();
        var container = GetContainer<TAggregate>();
        var metricContext = CreateReadEventDatabaseOperationRequestChargeMetricContext<TAggregate>(container);

        var records = InternalReadRecordsWithExceptionInterceptAsync<TAggregate>(aggregateId, container, activity, metricContext, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseReadActivity();
        var container = GetContainer<TAggregate>();
        var metricContext = CreateReadRecordsEventDatabaseOperationRequestChargeMetricContext<TAggregate>(container);

        var records = InternalReadRecordsWithExceptionInterceptAsync<TAggregate>(aggregateId, container, activity, metricContext, cancellationToken);
        await foreach (var record in records)
        {
            yield return record;
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseWriteActivity(retrievedVersion);
        var totalRequestCharge = 0.0;
        var operationResult = "failure";
        EventDatabaseOperationRequestChargeMetricContext? metricContext = null;

        try
        {
            if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));
            if (events == null) throw new ArgumentNullException(nameof(events));

            if (events.Count == 0)
            {
                return;
            }

            var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
            var container = GetContainer<TAggregate>();
            metricContext = CreateWriteEventDatabaseOperationRequestChargeMetricContext<TAggregate>(container, customProperties);

            activity.EnrichEventDatabaseWriteActivityWithStreamId(streamId);

            var originalRetrievedVersion = retrievedVersion;

            var retryCountForUnexpectedVersionWhenExpectedVersionIsAny = _cosmosConfiguration.RetryCountForUnexpectedVersionWhenExpectedVersionIsAny;

            var tryIndex = 0;
            while (tryIndex <= retryCountForUnexpectedVersionWhenExpectedVersionIsAny)
            {
                activity.EnrichEventDatabaseWriteActivityWithTryCount(tryIndex + 1);

                try
                {
                    await InternalWriteAsync<TAggregate>(aggregateId, streamId, container, events, retrievedVersion, expectedVersion, conversationId, initiatorId, customProperties, requestCharge => totalRequestCharge += requestCharge, cancellationToken);
                    operationResult = "success";
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
            if (totalRequestCharge > 0.0 && metricContext is not null)
            {
                EventDatabaseOperationRequestChargeMetric.Record(totalRequestCharge, operationResult, metricContext, EventDatabaseOperationRequestChargeMetricCustomPropertyTagNames);
            }

            activity?.Complete();
        }
    }

    public async Task DeleteAsync<TAggregate>(string aggregateId, EventsDeletionMode deletionMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId))
        {
            throw new ArgumentException(nameof(aggregateId));
        }

        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
        var container = GetContainer<TAggregate>();

        if (deletionMode == EventsDeletionMode.MarkAsDeleted)
        {
            var headerDocumentIds = await ReadHeaderDocumentIdsAsync(container, streamId, cancellationToken);
            foreach (var headerDocumentId in headerDocumentIds)
            {
                await DeleteStreamDocumentPermanentlyAsync(container, streamId, headerDocumentId, cancellationToken);
            }

            var eventDocumentIds = await ReadEventAndPacketDocumentIdsAsync(container, streamId, true, cancellationToken);
            foreach (var eventDocumentId in eventDocumentIds)
            {
                await MarkStreamDocumentAsDeletedAsync(container, streamId, eventDocumentId, cancellationToken);
            }

            return;
        }

        if (deletionMode == EventsDeletionMode.DeletePermanently)
        {
            var streamDocumentIds = await ReadStreamDocumentIdsAsync(container, streamId, true, false, cancellationToken);
            foreach (var streamDocumentId in streamDocumentIds)
            {
                await DeleteStreamDocumentPermanentlyAsync(container, streamId, streamDocumentId, cancellationToken);
            }

            return;
        }

        throw new EventForgingException($"Unknown events deletion mode: {deletionMode}.");
    }

    private IAsyncEnumerable<EventDatabaseRecord> InternalReadRecordsWithExceptionInterceptAsync<TAggregate>(string aggregateId, Container container, Activity? activity, EventDatabaseOperationRequestChargeMetricContext metricContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var records = InternalReadRecordsAsync<TAggregate>(aggregateId, container, activity, metricContext, cancellationToken);

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

    private async IAsyncEnumerable<EventDatabaseRecord> InternalReadRecordsAsync<TAggregate>(string aggregateId, Container container, Activity? activity, EventDatabaseOperationRequestChargeMetricContext metricContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));

        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);

        activity.EnrichEventDatabaseReadActivityWithStreamId(streamId);

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

        var operationResult = "failure";
        try
        {
            await foreach (var item in iterator)
            {
                if (item.TryHandleAs<EventDocument>(nameof(DocumentType.Event), out var eventDocument))
                {
                    if (eventDocument.IsDeleted == true)
                    {
                        continue;
                    }

                    var eventId = Guid.Parse(eventDocument.Id!);
                    var deserializedEventData = DeserializeEventData(eventDocument.StreamId!, eventDocument.Id!, eventId, eventDocument.Data, eventDocument.EventType);

                    yield return new EventDatabaseRecord(
                        eventId,
                        eventDocument.EventNumber,
                        eventDocument.EventType!,
                        DateTimeOffset.FromUnixTimeSeconds(eventDocument.Timestamp).UtcDateTime,
                        deserializedEventData,
                        eventDocument.Metadata?.ConversationId ?? Guid.Empty,
                        eventDocument.Metadata?.InitiatorId ?? Guid.Empty,
                        eventDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
                }

                if (item.TryHandleAs<EventsPacketDocument>(nameof(DocumentType.EventsPacket), out var eventsPacketDocument))
                {
                    if (eventsPacketDocument.IsDeleted == true)
                    {
                        continue;
                    }

                    foreach (var e in eventsPacketDocument.Events ?? throw new EventForgingException($"Events packet {eventsPacketDocument.Id ?? "NULL"} has no events."))
                    {
                        var deserializeEventData = DeserializeEventData(eventsPacketDocument.StreamId!, eventsPacketDocument.Id!, e.EventId, e.Data, e.EventType);

                        yield return new EventDatabaseRecord(
                            e.EventId,
                            e.EventNumber,
                            e.EventType!,
                            DateTimeOffset.FromUnixTimeSeconds(eventsPacketDocument.Timestamp).UtcDateTime,
                            deserializeEventData,
                            eventsPacketDocument.Metadata?.ConversationId ?? Guid.Empty,
                            eventsPacketDocument.Metadata?.InitiatorId ?? Guid.Empty,
                            eventsPacketDocument.Metadata?.CustomProperties ?? new Dictionary<string, string>());
                    }
                }
            }

            operationResult = "success";
        }
        finally
        {
            if (totalRequestCharge > 0.0)
            {
                EventDatabaseOperationRequestChargeMetric.Record(totalRequestCharge, operationResult, metricContext, EventDatabaseOperationRequestChargeMetricCustomPropertyTagNames);
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

    private async Task InternalWriteAsync<TAggregate>(string aggregateId, string streamId, Container container, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, Action<double> onRequestCharge, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartEventDatabaseWriteAttemptActivity(retrievedVersion);

        customProperties.StoreCurrentActivityId();

        try
        {
            activity?.EnrichEventDatabaseWriteAttemptActivityWithStreamId(streamId);

            var requestOptions = new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false, };
            var transaction = container.CreateTransactionalBatch(new PartitionKey(streamId));

            if (retrievedVersion.AggregateDoesNotExist)
            {
                transaction.CreateItem(CreateStreamHeaderDocument(streamId, events.Count), requestOptions);
            }
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
                {
                    expectedHeaderVersion = expectedVersion;
                }

                var headerPatchRequestOptions = new TransactionalBatchPatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    FilterPredicate = $"FROM x WHERE x.version = {expectedHeaderVersion}",
                };

                transaction.PatchItem(HeaderDocument.CreateId(streamId), [PatchOperation.Increment("/version", events.Count),], headerPatchRequestOptions);
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
            onRequestCharge(response.RequestCharge);
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(response);

            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {
                var alreadyWritten = await CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(streamId, initiatorId, activity, onRequestCharge, cancellationToken);
                if (alreadyWritten)
                {
                    _logger.WriteIgnoredDueToIdempotencyCheck(streamId, initiatorId);
                    return;
                }

                var actualVersion = await ReadCurrentVersionAsync<TAggregate>(streamId, activity, onRequestCharge, cancellationToken);

                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new EventForgingException(response.ErrorMessage);
            }
        }
        catch (CosmosException ex)
        {
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
            activity?.RecordException(ex);
            throw;
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

    private async Task<int> ReadCurrentVersionAsync<TAggregate>(string streamId, Activity? activity, Action<double> onRequestCharge, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetContainer<TAggregate>().ReadItemAsync<HeaderDocument>(HeaderDocument.CreateId(streamId), new PartitionKey(streamId), cancellationToken: cancellationToken);

            var currentVersion = result.Resource.Version;

            activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("Current version of the aggregate has been read.", result.StatusCode, result.RequestCharge, new Dictionary<string, string> { { TracingAttributeNames.AggregateVersion, currentVersion.ToString() }, });
            onRequestCharge(result.RequestCharge);

            return currentVersion;
        }
        catch (CosmosException ex) when (ex.StatusCode == (HttpStatusCode)429)
        {
            activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("An exception occurred during the read of the current version of the aggregate.", ex.StatusCode, ex.RequestCharge);
            onRequestCharge(ex.RequestCharge);
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
            throw;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent("An exception occurred during the read of the current version of the aggregate.", ex.StatusCode, ex.RequestCharge);
            onRequestCharge(ex.RequestCharge);

            throw new EventForgingStreamNotFoundException(streamId, ex);
        }
    }

    private async Task<bool> CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(string streamId, Guid initiatorId, Activity? activity, Action<double> onRequestCharge, CancellationToken cancellationToken = default)
    {
        if (initiatorId == Guid.Empty)
            return false;

        if (_configuration.IdempotencyEnabled)
        {
            var firstDocumentId = IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, 0).ToString();
            ResponseMessage response;
            try
            {
                response = await GetContainer<TAggregate>().ReadItemStreamAsync(firstDocumentId, new PartitionKey(streamId), cancellationToken: cancellationToken);
            }
            catch (CosmosException ex)
            {
                onRequestCharge(ex.RequestCharge);
                EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
                throw;
            }

            using (response)
            {
                onRequestCharge(response.Headers.RequestCharge);
                EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(response);

                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new EventForgingException($"The idempotency verification failed while checking whether events for initiatorId '{initiatorId}' were already written to stream '{streamId}'. Cosmos DB point read for document '{firstDocumentId}' returned status code {response.StatusCode} with message: {response.ErrorMessage}");
                }

                var checkResult = response.StatusCode != HttpStatusCode.NotFound;

                activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent(
                    "The idempotency check associated with the given initiatorId has been successfully completed.",
                    response.StatusCode,
                    response.Headers.RequestCharge,
                    new Dictionary<string, string>
                    {
                        { TracingAttributeNames.InitiatorId, initiatorId.ToString() },
                        { CosmosDbTracingAttributeNames.EventDatabaseWriteIdempotencyCheckResult, checkResult.ToString().ToLower() },
                    });
                return checkResult;
            }
        }

        var query = new QueryDefinition($"SELECT TOP 1 VALUE c.id FROM c WHERE c.metadata.initiatorId = @initiatorId AND (c.documentType = '{DocumentType.Event}' OR c.documentType = '{DocumentType.EventsPacket}')")
            .WithParameter("@initiatorId", initiatorId.ToString());
        var iterator = GetContainer<TAggregate>().GetItemQueryIterator<string>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = 1, });

        FeedResponse<string> page;
        try
        {
            page = await iterator.ReadNextAsync(cancellationToken);
        }
        catch (CosmosException ex)
        {
            onRequestCharge(ex.RequestCharge);
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
            throw;
        }

        var fallbackCheckResult = page.Any();

        activity?.RecordEventDatabaseWriteAttemptActivityAdditionalDbOperationEvent(
            "The idempotency check associated with the given initiatorId has been successfully completed.",
            page.StatusCode,
            page.RequestCharge,
            new Dictionary<string, string>
            {
                { TracingAttributeNames.InitiatorId, initiatorId.ToString() },
                { CosmosDbTracingAttributeNames.EventDatabaseWriteIdempotencyCheckResult, fallbackCheckResult.ToString().ToLower() },
            });
        onRequestCharge(page.RequestCharge);

        return fallbackCheckResult;
    }

    private static async Task<IReadOnlyList<string>> ReadHeaderDocumentIdsAsync(Container container, string streamId, CancellationToken cancellationToken)
    {
        return await ReadDocumentIdsByConditionAsync(container, streamId, $"c.documentType = '{DocumentType.Header}'", cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadEventAndPacketDocumentIdsAsync(Container container, string streamId, bool onlyNotDeleted, CancellationToken cancellationToken)
    {
        return await ReadStreamDocumentIdsAsync(container, streamId, false, onlyNotDeleted, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadStreamDocumentIdsAsync(Container container, string streamId, bool includeHeader, bool onlyNotDeleted, CancellationToken cancellationToken)
    {
        var notDeletedCondition = onlyNotDeleted ? " AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted != true)" : string.Empty;
        var typeCondition = includeHeader
            ? $"(c.documentType = '{DocumentType.Header}' OR c.documentType = '{DocumentType.Event}' OR c.documentType = '{DocumentType.EventsPacket}')"
            : $"(c.documentType = '{DocumentType.Event}' OR c.documentType = '{DocumentType.EventsPacket}')";
        return await ReadDocumentIdsByConditionAsync(container, streamId, $"{typeCondition}{notDeletedCondition}", cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadDocumentIdsByConditionAsync(Container container, string streamId, string whereCondition, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition($"SELECT VALUE c.id FROM c WHERE {whereCondition}");
        var queryRequestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(streamId),
            MaxItemCount = -1,
        };
        var iterator = container.GetItemQueryIterator<string>(query, requestOptions: queryRequestOptions);

        var documentIds = new List<string>();
        while (iterator.HasMoreResults)
        {
            FeedResponse<string> page;
            try
            {
                page = await iterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
                throw;
            }

            foreach (var documentId in page)
            {
                if (!string.IsNullOrWhiteSpace(documentId))
                {
                    documentIds.Add(documentId);
                }
            }
        }

        return documentIds;
    }

    private static async Task MarkStreamDocumentAsDeletedAsync(Container container, string streamId, string documentId, CancellationToken cancellationToken)
    {
        try
        {
            await container.PatchItemAsync<object>(
                documentId,
                new PartitionKey(streamId),
                new[] { PatchOperation.Set("/isDeleted", true), },
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
            throw;
        }
    }

    private static async Task DeleteStreamDocumentPermanentlyAsync(Container container, string streamId, string documentId, CancellationToken cancellationToken)
    {
        try
        {
            await container.DeleteItemAsync<object>(documentId, new PartitionKey(streamId), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
            throw;
        }
    }

    private Container GetContainer<TAggregate>()
    {
        return _cosmosDbProvider.GetAggregateContainer<TAggregate>();
    }

    private static EventDatabaseOperationRequestChargeMetricContext CreateReadEventDatabaseOperationRequestChargeMetricContext<TAggregate>(Container container)
    {
        return new EventDatabaseOperationRequestChargeMetricContext("read", "read", typeof(TAggregate), container.Id, null);
    }

    private static EventDatabaseOperationRequestChargeMetricContext CreateReadRecordsEventDatabaseOperationRequestChargeMetricContext<TAggregate>(Container container)
    {
        return new EventDatabaseOperationRequestChargeMetricContext("read", "read_records", typeof(TAggregate), container.Id, null);
    }

    private static EventDatabaseOperationRequestChargeMetricContext CreateWriteEventDatabaseOperationRequestChargeMetricContext<TAggregate>(Container container, IDictionary<string, string> customProperties)
    {
        return new EventDatabaseOperationRequestChargeMetricContext("write", "write", typeof(TAggregate), container.Id, customProperties);
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
