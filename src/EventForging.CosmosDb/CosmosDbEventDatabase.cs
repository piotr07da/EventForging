using System.Net;
using System.Runtime.CompilerServices;
using EventForging.Idempotency;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventDatabase : IEventDatabase
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventForgingConfiguration _configuration;
    private readonly ILogger _logger;

    public CosmosDbEventDatabase(
        ICosmosDbProvider cosmosDbProvider,
        IStreamNameFactory streamNameFactory,
        IEventForgingConfiguration configuration,
        ILoggerFactory? loggerFactory = null)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = loggerFactory.CreateEventForgingLogger();
    }

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var iterator = GetContainer<TAggregate>().GetItemQueryIterator<EventDocument>($"SELECT * FROM x WHERE x.documentType = '{nameof(DocumentType.Event)}' ORDER BY x.eventNumber", requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            var events = page.Select(ed => ed.Data ?? throw new EventForgingException($"Event {ed.Id ?? "NULL"} has no data.")).ToArray();
            foreach (var e in events)
            {
                yield return e;
            }
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));
        if (events == null) throw new ArgumentNullException(nameof(events));
        if (events.Count > 99)
        {
            // https://docs.microsoft.com/en-us/azure/cosmos-db/sql/transactional-batch
            // "Cosmos DB transactions support a maximum of 100 operations. One operation is reserved for stream metadata write. As a result, a maximum of 99 events can be saved.
            throw new ArgumentOutOfRangeException(nameof(events), "Max number of events is 99.");
        }

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var requestOptions = new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false, };
        var transaction = GetContainer<TAggregate>().CreateTransactionalBatch(new PartitionKey(streamId));

        if (lastReadAggregateVersion.AggregateDoesNotExist)
        {
            transaction.CreateItem(CreateStreamHeaderDocument(streamId, events.Count), requestOptions);
        }
        else
        {
            long expectedHeaderVersion;
            if (expectedVersion.IsAny)
            {
                expectedHeaderVersion = lastReadAggregateVersion;
            }
            else if (expectedVersion.IsNone)
            {
                // Because this is the case in which lastReadAggregateVersion.AggregateExists is true then this case (expectedVersion.IsNone) will never occur
                // due to the check performed in the Repository class (lastReadAggregateVersion.AggregateExists && expectedVersion.IsNone already throws exception).
                expectedHeaderVersion = -1L;
            }
            else
            {
                expectedHeaderVersion = expectedVersion;
            }

            transaction.PatchItem(HeaderDocument.CreateId(streamId), new[] { PatchOperation.Increment("/version", events.Count), }, new TransactionalBatchPatchItemRequestOptions { EnableContentResponseOnWrite = false, FilterPredicate = $"FROM x WHERE x.version = {expectedHeaderVersion}", });
        }

        var eventItems = events.Select((e, eIx) =>
        {
            var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();
            return CreateStreamEventDocument(streamId, eventId, lastReadAggregateVersion + eIx + 1L, e, conversationId, initiatorId, customProperties);
        });

        foreach (var eventItem in eventItems)
        {
            transaction.CreateItem(eventItem, requestOptions);
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

            throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, lastReadAggregateVersion, headerItem.Version);
        }

        if (!response.IsSuccessStatusCode)
            throw new Exception(response.ErrorMessage);
    }

    private async Task<HeaderDocument> ReadHeaderAsync<TAggregate>(string streamId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetContainer<TAggregate>().ReadItemAsync<HeaderDocument>(HeaderDocument.CreateId(streamId), new PartitionKey(streamId), cancellationToken: cancellationToken);
            return result.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new EventForgingStreamNotFoundException(streamId, ex);
        }
    }

    private async Task<bool> CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(string aggregateId, Guid initiatorId, CancellationToken cancellationToken = default)
    {
        if (initiatorId == Guid.Empty)
        {
            return false;
        }

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.documentType = 'Event' AND c.metadata.initiatorId = @initiatorId")
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
