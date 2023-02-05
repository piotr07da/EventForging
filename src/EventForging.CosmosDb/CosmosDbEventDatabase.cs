using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventDatabase : IEventDatabase
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventForgingCosmosDbConfiguration _configuration;
    private readonly ILogger _logger;

    public CosmosDbEventDatabase(
        ICosmosDbProvider cosmosDbProvider,
        IStreamNameFactory streamNameFactory,
        IEventForgingCosmosDbConfiguration configuration,
        ILoggerFactory? loggerFactory = null)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = loggerFactory.CreateEventForgingLogger();
    }

    public async Task ReadAsync<TAggregate>(string aggregateId, IEventDatabaseReadCallback callback, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aggregateId)) throw new ArgumentException(nameof(aggregateId));

        var streamId = _streamNameFactory.Create(typeof(TAggregate), aggregateId);

        var iterator = GetContainer<TAggregate>().GetItemQueryIterator<EventDocument>($"SELECT * FROM x WHERE x.documentType = '{nameof(DocumentType.Event)}' ORDER BY x.eventNumber", requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(streamId), MaxItemCount = -1, });

        callback.OnBegin();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);

            var events = page.Select(p => p.Data).ToArray();
            callback.OnRead(events);
        }

        callback.OnEnd();
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

        HeaderDocument headerItem;
        var isNew = false;

        if (lastReadAggregateVersion.AggregateDoesNotExist)
        {
            if (!expectedVersion.IsNone && !expectedVersion.IsAny)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, lastReadAggregateVersion, null);
            }

            headerItem = CreateStreamHeaderDocument(streamId);
            isNew = true;
        }
        else
        {
            if (expectedVersion.IsNone)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, lastReadAggregateVersion, null);
            }

            headerItem = await ReadHeaderAsync<TAggregate>(streamId, cancellationToken);

            if (!expectedVersion.IsAny && headerItem.Version != expectedVersion)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, lastReadAggregateVersion, headerItem.Version);
            }

            if (expectedVersion.IsAny && headerItem.Version != lastReadAggregateVersion)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, lastReadAggregateVersion, headerItem.Version);
            }

            var alreadyWritten = await CheckIfContainsAnyEventForGivenInitiatorIdAsync<TAggregate>(aggregateId, initiatorId, cancellationToken);
            if (alreadyWritten)
            {
                LogIdempotencyWarning(aggregateId, initiatorId);
            }
        }

        var currentVersion = headerItem.Version;

        headerItem.Version += events.Count;

        var requestOptions = new TransactionalBatchItemRequestOptions { EnableContentResponseOnWrite = false, };
        var transaction = GetContainer<TAggregate>().CreateTransactionalBatch(new PartitionKey(streamId));

        if (isNew)
        {
            transaction.CreateItem(headerItem, requestOptions);
        }
        else
        {
            transaction.ReplaceItem(headerItem.Id, headerItem, new TransactionalBatchItemRequestOptions { IfMatchEtag = headerItem.ETag, EnableContentResponseOnWrite = false, });
        }

        var eventItems = events.Select((e, i) => CreateStreamEventDocument(streamId, currentVersion + i + 1, e, conversationId, initiatorId, customProperties));

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
                LogIdempotencyWarning(aggregateId, initiatorId);
                return;
            }

            headerItem = await ReadHeaderAsync<HeaderDocument>(streamId, cancellationToken);

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
        return _cosmosDbProvider.GetContainer<TAggregate>();
    }


    private static EventDocument CreateStreamEventDocument(string streamId, int eventNumber, object eventData, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
    {
        return new EventDocument(streamId, eventNumber, eventData, new EventMetadata(conversationId, initiatorId, customProperties));
    }

    private static HeaderDocument CreateStreamHeaderDocument(string streamId)
    {
        return new HeaderDocument(streamId);
    }

    private void LogIdempotencyWarning(string aggregateId, Guid initiatorId)
    {
        _logger.LogWarning($"Cannot write events for aggregate {aggregateId} because these events has already been written for the same initiatorId {initiatorId}.");
    }
}
