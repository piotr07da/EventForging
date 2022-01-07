using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventForging.InMemory
{
    public class InMemoryEventDatabase : IEventDatabase
    {
        private readonly ConcurrentDictionary<string, object[]> _streams = new();

        public Task<IEnumerable<object>> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            _streams.TryGetValue(aggregateId, out var events);
            events = events ?? Array.Empty<object>();
            return Task.FromResult(events.AsEnumerable());
        }

        public Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
        {
            _streams.TryGetValue(aggregateId, out var currentEvents);
            currentEvents ??= Array.Empty<object>();

            var actualVersion = currentEvents.Length - 1;

            if ((lastReadAggregateVersion.AggregateDoesNotExist && currentEvents.Length > 0) || (lastReadAggregateVersion.AggregateExists && lastReadAggregateVersion.Value != actualVersion))
            {
                throw new EventForgingUnexpectedVersionException(expectedVersion, lastReadAggregateVersion, actualVersion);
            }

            var allEvents = currentEvents.ToList();
            allEvents.AddRange(events);
            _streams[aggregateId] = allEvents.ToArray();
            return Task.CompletedTask;
        }
    }
}
