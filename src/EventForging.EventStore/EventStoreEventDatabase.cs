using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using EventForging.Idempotency;
using EventForging.Serialization;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventDatabase : IEventDatabase
{
    private readonly EventStoreClient _client;
    private readonly IEventForgingConfiguration _configuration;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public EventStoreEventDatabase(
        IEventForgingConfiguration configuration,
        IStreamNameFactory streamNameFactory,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider,
        EventStoreClient client)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = _streamNameFactory.Create(typeof(TAggregate), aggregateId);
        await foreach (var re in _client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: cancellationToken))
        {
            var ed = _eventSerializer.DeserializeFromBytes(re.Event.EventType, re.Event.Data.ToArray());
            yield return ed;
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var streamName = _streamNameFactory.Create(typeof(TAggregate), aggregateId);
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
                await _client.AppendToStreamAsync(streamName, sv.Value, eventsData, cancellationToken: cancellationToken);
            }
            else
            {
                await _client.AppendToStreamAsync(streamName, StreamState.Any, eventsData, cancellationToken: cancellationToken);
            }
        }
        catch (WrongExpectedVersionException e)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, streamName, expectedVersion, retrievedVersion, e.ActualStreamRevision.ToInt64(), e);
        }
    }
}
