using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using EventForging.EventsHandling;
using EventForging.Idempotency;
using EventForging.InMemory.EventHandling;
using EventForging.Serialization;

namespace EventForging.InMemory;

internal sealed class InMemoryEventDatabase : IEventDatabase
{
    private static readonly ConcurrentDictionary<string, IDictionary<Guid, EventEntry>> _streams = new();
    private readonly IEventSerializer _serializer;
    private readonly IEventForgingConfiguration _configuration;
    private readonly IEventForgingInMemoryConfiguration _inMemoryConfiguration;
    private readonly ISubscriptions _subscriptions;

    public InMemoryEventDatabase(IEventSerializer serializer, IEventForgingConfiguration configuration, IEventForgingInMemoryConfiguration inMemoryConfiguration, ISubscriptions subscriptions)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _inMemoryConfiguration = inMemoryConfiguration ?? throw new ArgumentNullException(nameof(inMemoryConfiguration));
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
    }

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _streams.TryGetValue(aggregateId, out var eventEntries);
        eventEntries ??= new Dictionary<Guid, EventEntry>();
        foreach (var entry in eventEntries.Values.OrderBy(e => e.Version))
        {
            object eData;

            if (_inMemoryConfiguration.SerializationEnabled)
            {
                eData = _serializer.DeserializeFromBytes(entry.Type, (entry.Data as byte[])!);
            }
            else
            {
                eData = entry.Data;
            }


            yield return eData;
        }

        await Task.CompletedTask;
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        _streams.TryGetValue(aggregateId, out var currentEventEntries);
        currentEventEntries ??= new Dictionary<Guid, EventEntry>();

        var actualVersion = currentEventEntries.Values.Count - 1;

        var allEventEntries = currentEventEntries.ToDictionary(de => de.Key, de => de.Value);
        var newEventEntries = new List<EventEntry>();
        for (var eIx = 0; eIx < events.Count; ++eIx)
        {
            var eventId = _configuration.IdempotencyEnabled ? IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, eIx) : Guid.NewGuid();
            if (allEventEntries.ContainsKey(eventId))
            {
                continue;
            }

            var e = events[eIx];

            object eData;
            string eventType;
            if (_inMemoryConfiguration.SerializationEnabled)
            {
                eData = _serializer.SerializeToBytes(e, out eventType);
            }
            else
            {
                eData = e;
                eventType = e.GetType().FullName!;
            }

            var entry = new EventEntry(eventId, actualVersion + eIx + 1, eventType, DateTime.UtcNow, eData, new EventMetadata(conversationId, initiatorId));

            newEventEntries.Add(entry);
        }

        if (newEventEntries.Any())
        {
            long expectedVersionNumber;
            if (expectedVersion.IsAny)
            {
                expectedVersionNumber = lastReadAggregateVersion;
            }
            else if (expectedVersion.IsNone)
            {
                expectedVersionNumber = -1;
            }
            else
            {
                expectedVersionNumber = expectedVersion;
            }

            if (expectedVersionNumber != actualVersion)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, aggregateId, expectedVersion, lastReadAggregateVersion, actualVersion);
            }

            foreach (var newEventEntry in newEventEntries)
            {
                allEventEntries.Add(newEventEntry.Id, newEventEntry);
            }
        }

        _streams[aggregateId] = allEventEntries;

        PublishEvents(newEventEntries, cancellationToken);

        await Task.CompletedTask;
    }

    private void PublishEvents(IReadOnlyCollection<EventEntry> eventEntries, CancellationToken cancellationToken)
    {
        foreach (var subscription in _inMemoryConfiguration.EventSubscriptions)
        {
            foreach (var entry in eventEntries)
            {
                object eData;
                if (_inMemoryConfiguration.SerializationEnabled)
                {
                    eData = _serializer.DeserializeFromBytes(entry.Type, (entry.Data as byte[])!);
                }
                else
                {
                    eData = entry.Data;
                }

                _subscriptions.Send(subscription, eData, new EventInfo(entry.Id, entry.Version, entry.Type, entry.Metadata.ConversationId, entry.Metadata.InitiatorId, entry.Timestamp, entry.Metadata.CustomProperties));
            }
        }
    }
}
