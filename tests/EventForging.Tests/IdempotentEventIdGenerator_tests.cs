// ReSharper disable InconsistentNaming

using EventForging.Idempotency;
using Xunit;

namespace EventForging.Tests
{
    public class IdempotentEventIdGenerator_tests
    {
        [Fact]
        public void all_generated_event_ids_from_a_single_initiatorId_shall_not_be_equal()
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

        [Fact]
        public void initiatorId_cannot_be_empty()
        {
            Assert.Throws<EventForgingException>(() => IdempotentEventIdGenerator.GenerateIdempotentEventId(Guid.Empty, 0));
        }
    }
}
