using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventForging.Serialization;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventDatabase : IEventDatabase
{
    private readonly IEventStoreClientProvider _eventStoreClientProvider;
    private readonly IEventForgingEventStoreConfiguration _configuration;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public EventStoreEventDatabase(
        IEventStoreClientProvider eventStoreClientProvider,
        IEventForgingEventStoreConfiguration configuration,
        IStreamNameFactory streamNameFactory,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider)
    {
        _eventStoreClientProvider = eventStoreClientProvider ?? throw new ArgumentNullException(nameof(eventStoreClientProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public async Task ReadAsync<TAggregate>(string aggregateId, IEventDatabaseReadCallback callback, CancellationToken cancellationToken = default)
    {
        var client = _eventStoreClientProvider.GetClient();

        var streamName = _streamNameFactory.Create(typeof(TAggregate), aggregateId);
        await foreach (var re in client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: cancellationToken))
        {
            var ed = _eventSerializer.DeserializeFromBytes(re.Event.EventType, re.Event.Data.ToArray());
            callback.OnRead(ed);
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        var client = _eventStoreClientProvider.GetClient();

        var streamName = _streamNameFactory.Create(typeof(TAggregate), aggregateId);
        var eventsData = events.Select((e, eIx) =>
        {
            var eventData = _eventSerializer.SerializeToBytes(e, out var eventTypeName);
            var eventMetadataJson = JsonSerializer.Serialize(new EventMetadata(conversationId, initiatorId, customProperties), JsonSerializerOptions);
            var eventMetadata = Encoding.UTF8.GetBytes(eventMetadataJson);

            var eventId = GenerateEventId(initiatorId, eIx);
            return new EventData(eventId, eventTypeName, eventData, eventMetadata);
        });

        try
        {
            if (expectedVersion.IsAny)
            {
                await client.AppendToStreamAsync(streamName, StreamRevision.FromInt64(lastReadAggregateVersion), eventsData, cancellationToken: cancellationToken);
            }
            else
            {
                var sv = expectedVersion == ExpectedVersion.None ? StreamRevision.None : StreamRevision.FromInt64(expectedVersion);
                await client.AppendToStreamAsync(streamName, sv, eventsData, cancellationToken: cancellationToken);
            }
        }
        catch (WrongExpectedVersionException e)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, streamName, expectedVersion, lastReadAggregateVersion, e.ActualStreamRevision.ToInt64(), e);
        }
    }

    private Uuid GenerateEventId(Guid initiatorId, int eventIndex)
    {
        switch (_configuration.IdempotencyMode)
        {
            case IdempotencyMode.Disabled:
                return Uuid.NewUuid();

            case IdempotencyMode.BasedOnInitiatorId:
                return GenerateIdempotentEventId(initiatorId, eventIndex);

            default:
                throw new EventForgingConfigurationException($"Unsupported idempotency mode {_configuration.IdempotencyMode}.");
        }
    }

    private static Uuid GenerateIdempotentEventId(Guid initiatorId, int eventIndex)
    {
        if (initiatorId == Guid.Empty)
        {
            throw new EventForgingException($"If the idempotency mode was selected to be {nameof(IdempotencyMode.BasedOnInitiatorId)}, then initiatorId cannot be equal to an empty Guid.");
        }

        var initiatorIdBytes = initiatorId.ToByteArray();

        for (var i = 0; i < initiatorIdBytes.Length; ++i)
        {
            var b = initiatorIdBytes[i];
            b = (byte)(b ^ 0b11010100);
            if (i < 4) // eventIndex is of type int so there are 4 bytes
            {
                var eventIndexMask = (byte)(eventIndex >> 8);
                b = (byte)(b ^ eventIndexMask);
            }

            initiatorIdBytes[i] = b;
        }

        var eventId = new Guid(initiatorIdBytes);
        return Uuid.FromGuid(eventId);
    }
}
