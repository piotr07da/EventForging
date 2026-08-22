using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EventForging.Diagnostics.Tracing;
using EventForging.Idempotency;
using EventForging.Serialization;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventDatabase : IEventDatabase
{
    private readonly EventStoreClient _client;
    private readonly IEventForgingConfiguration _configuration;
    private readonly IStreamIdFactory _streamIdFactory;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public EventStoreEventDatabase(
        IEventForgingConfiguration configuration,
        IStreamIdFactory streamIdFactory,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider,
        EventStoreClient client)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _streamIdFactory = streamIdFactory ?? throw new ArgumentNullException(nameof(streamIdFactory));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default) =>
        ReadAsync<TAggregate>(aggregateId, EventStreamReadPosition.Beginning, cancellationToken);

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsFromPositionAsync<TAggregate>(aggregateId, readPosition, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default) =>
        ReadRecordsFromPositionAsync<TAggregate>(aggregateId, EventStreamReadPosition.Beginning, cancellationToken);

    private async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsFromPositionAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
        var startPosition = readPosition.TryGetAfterVersion(out var afterVersion)
            ? StreamPosition.FromInt64(afterVersion.Next().Value)
            : StreamPosition.Start;
        await foreach (var re in _client.ReadStreamAsync(Direction.Forwards, streamId, startPosition, cancellationToken: cancellationToken))
        {
            var ed = _eventSerializer.DeserializeFromBytes(re.Event.EventType, re.Event.Data.ToArray());
            var eventMetadataJson = Encoding.UTF8.GetString(re.Event.Metadata.ToArray());
            var em = JsonSerializer.Deserialize<EventMetadata>(eventMetadataJson, JsonSerializerOptions)!;
            yield return new EventDatabaseRecord(
                re.Event.EventId.ToGuid(),
                re.Event.EventNumber.ToInt64(),
                re.Event.EventType,
                re.Event.Created,
                ed,
                em.ConversationId,
                em.InitiatorId,
                em.CustomProperties ?? new Dictionary<string, string>());
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        customProperties.StoreCurrentActivityId();

        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
        var eventsData = events.Select((e, eIx) =>
        {
            var eventData = _eventSerializer.SerializeToBytes(e, out var eventTypeName);
            var eventMetadataJson = JsonSerializer.Serialize(new EventMetadata(conversationId, initiatorId, customProperties), JsonSerializerOptions);
            var eventMetadata = Encoding.UTF8.GetBytes(eventMetadataJson);

            var eventId = _configuration.IdempotencyEnabled ? Uuid.FromGuid(IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx)) : Uuid.NewUuid();
            return new EventData(eventId, eventTypeName, eventData, eventMetadata);
        });

        try
        {
            StreamRevision? sv;
            if (expectedVersion.IsAny)
            {
                sv = null;
            }
            else if (expectedVersion.IsRetrieved)
            {
                sv = StreamRevision.FromInt64(retrievedVersion);
            }
            else if (expectedVersion.IsNone)
            {
                sv = StreamRevision.None;
            }
            else
            {
                sv = StreamRevision.FromInt64(expectedVersion);
            }

            if (sv.HasValue)
            {
                await _client.AppendToStreamAsync(streamId, sv.Value, eventsData, cancellationToken: cancellationToken);
            }
            else
            {
                await _client.AppendToStreamAsync(streamId, StreamState.Any, eventsData, cancellationToken: cancellationToken);
            }
        }
        catch (WrongExpectedVersionException e)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, retrievedVersion, e.ActualStreamRevision.ToInt64(), e);
        }
    }
}
