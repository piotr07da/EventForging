﻿using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using EventForging.Diagnostics.Tracing;
using EventForging.EventsHandling;
using EventForging.Idempotency;
using EventForging.InMemory.EventHandling;
using EventForging.Serialization;

namespace EventForging.InMemory;

internal sealed class InMemoryEventDatabase : IEventDatabase
{
    private static readonly ConcurrentDictionary<string, IDictionary<Guid, EventEntry>> _streams = new();
    private readonly IStreamIdFactory _streamIdFactory;
    private readonly IEventSerializer _serializer;
    private readonly IEventForgingConfiguration _configuration;
    private readonly IInMemoryEventForgingConfiguration _inMemoryConfiguration;
    private readonly ISubscriptions _subscriptions;

    public InMemoryEventDatabase(IStreamIdFactory streamIdFactory, IEventSerializer serializer, IEventForgingConfiguration configuration, IInMemoryEventForgingConfiguration inMemoryConfiguration, ISubscriptions subscriptions)
    {
        _streamIdFactory = streamIdFactory ?? throw new ArgumentNullException(nameof(streamIdFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _inMemoryConfiguration = inMemoryConfiguration ?? throw new ArgumentNullException(nameof(inMemoryConfiguration));
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
    }

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsAsync<TAggregate>(aggregateId, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
        _streams.TryGetValue(streamId, out var eventEntries);
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

            yield return new EventDatabaseRecord(
                entry.Id,
                entry.Version,
                entry.Type,
                entry.Timestamp,
                eData,
                entry.Metadata.ConversationId,
                entry.Metadata.InitiatorId,
                entry.Metadata.CustomProperties ?? new Dictionary<string, string>());
        }

        await Task.CompletedTask;
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        customProperties.StoreCurrentActivityId();

        var streamId = _streamIdFactory.Create(typeof(TAggregate), aggregateId);
        _streams.TryGetValue(streamId, out var currentEventEntries);
        currentEventEntries ??= new Dictionary<Guid, EventEntry>();

        var currentVersion = currentEventEntries.Values.Count - 1;

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

            var entry = new EventEntry(streamId, eventId, currentVersion + eIx + 1, eventType, DateTime.UtcNow, eData, new EventMetadata(conversationId, initiatorId, customProperties));

            newEventEntries.Add(entry);
        }

        if (newEventEntries.Any())
        {
            long? expectedVersionNumber;
            if (expectedVersion.IsAny)
            {
                expectedVersionNumber = null;
            }
            else if (expectedVersion.IsRetrieved)
            {
                expectedVersionNumber = retrievedVersion;
            }
            else if (expectedVersion.IsNone)
            {
                expectedVersionNumber = -1;
            }
            else
            {
                expectedVersionNumber = expectedVersion;
            }

            if (expectedVersionNumber.HasValue && expectedVersionNumber.Value != currentVersion)
            {
                throw new EventForgingUnexpectedVersionException(aggregateId, streamId, expectedVersion, retrievedVersion, currentVersion);
            }

            foreach (var newEventEntry in newEventEntries)
            {
                allEventEntries.Add(newEventEntry.Id, newEventEntry);
            }
        }

        _streams[streamId] = allEventEntries;

        PublishEvents(newEventEntries);

        await Task.CompletedTask;
    }

    private void PublishEvents(IReadOnlyCollection<EventEntry> eventEntries)
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

                _subscriptions.Send(subscription, eData, new EventInfo(entry.StreamId, entry.Id, entry.Version, entry.Type, entry.Metadata.ConversationId, entry.Metadata.InitiatorId, entry.Timestamp, entry.Metadata.CustomProperties ?? new Dictionary<string, string>()));
            }
        }
    }
}
