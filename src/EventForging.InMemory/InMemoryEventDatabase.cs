using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace EventForging.InMemory;

internal sealed class InMemoryEventDatabase : IEventDatabase
{
    private readonly ConcurrentDictionary<string, object[]> _streams = new();

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _streams.TryGetValue(aggregateId, out var events);
        events = events ?? Array.Empty<object>();
        foreach (var e in events)
        {
            yield return e;
        }

        await Task.CompletedTask;
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        _streams.TryGetValue(aggregateId, out var currentEvents);
        currentEvents ??= Array.Empty<object>();

        var actualVersion = currentEvents.Length - 1;

        if ((lastReadAggregateVersion.AggregateDoesNotExist && currentEvents.Length > 0) || (lastReadAggregateVersion.AggregateExists && lastReadAggregateVersion.Value != actualVersion))
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, lastReadAggregateVersion, actualVersion);
        }

        var allEvents = currentEvents.ToList();
        allEvents.AddRange(events);
        _streams[aggregateId] = allEvents.ToArray();

        await Task.CompletedTask;
    }
}
