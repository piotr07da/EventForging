// ReSharper disable InconsistentNaming

using EventForging.Idempotency;
using Xunit;

namespace EventForging.Tests
{
    public class IdempotentEventIdGenerator_tests
    {
        [Fact]
        public void generated_event_ids_from_single_initiatorId_shall_be_different()
        {
            var initiatorId = Guid.NewGuid();

            var eventIds = new HashSet<Guid>();

            for (var i = 0; i < 15000; ++i)
            {
                var eventId = IdempotentEventIdGenerator.GenerateIdempotentEventId(initiatorId, i);
                Assert.DoesNotContain(eventId, eventIds);
                eventIds.Add(eventId);
            }
        }
    }
}
