using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventForging.Serialization;
using EventStore.Client;

namespace EventForging.EventStore;

internal sealed class EventStoreEventDatabase : IEventDatabase
{
    private readonly IEventStoreClientProvider _eventStoreClientProvider;
    private readonly IStreamNameFactory _streamNameFactory;
    private readonly IEventSerializer _eventSerializer;

    public EventStoreEventDatabase(
        IEventStoreClientProvider eventStoreClientProvider,
        IStreamNameFactory streamNameFactory,
        IEventSerializer eventSerializer)
    {
        _eventStoreClientProvider = eventStoreClientProvider ?? throw new ArgumentNullException(nameof(eventStoreClientProvider));
        _streamNameFactory = streamNameFactory ?? throw new ArgumentNullException(nameof(streamNameFactory));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
    }

    public async Task ReadAsync<TAggregate>(string aggregateId, IEventDatabaseReadCallback callback, CancellationToken cancellationToken = default)
    {
        var client = await _eventStoreClientProvider.GetClientAsync();

        var streamName = _streamNameFactory.Create(typeof(TAggregate), aggregateId);
        await foreach (var re in client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: cancellationToken))
        {
            var ed = _eventSerializer.DeserializeFromBytes(re.Event.EventType, re.Event.Data.ToArray());
            callback.OnRead(ed);
        }
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
    }
}

internal interface IEventStoreClientProvider
{
    Task<EventStoreClient> GetClientAsync();
}
