using EventForging.EventsHandling;
using Xunit;

namespace EventForging.Tests;

// ReSharper disable once InconsistentNaming
public sealed class ReceivedEventsBatch_tests
{
    private const string StreamId = "streamId1";

    [Fact]
    public void given_events_from_stream_when_batch_created_then_stream_id_is_available()
    {
        var batch = new ReceivedEventsBatch(StreamId, [CreateReceivedEvent(StreamId)]);

        Assert.Equal(StreamId, batch.StreamId);
    }

    [Fact]
    public void given_events_from_stream_when_batch_created_with_obsolete_constructor_then_stream_id_is_available()
    {
#pragma warning disable CS0618
        var batch = new ReceivedEventsBatch([CreateReceivedEvent(StreamId)]);
#pragma warning restore CS0618

        Assert.Equal(StreamId, batch.StreamId);
    }

    [Fact]
    public void given_no_events_when_batch_created_then_exception_is_thrown()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ReceivedEventsBatch(StreamId, []));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void given_event_from_different_stream_when_batch_created_then_exception_is_thrown()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ReceivedEventsBatch(StreamId, [CreateReceivedEvent("streamId2")]));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void given_source_collection_changed_after_batch_created_then_batch_remains_unchanged()
    {
        var receivedEvents = new List<ReceivedEvent> { CreateReceivedEvent(StreamId), };
        var batch = new ReceivedEventsBatch(StreamId, receivedEvents);

        receivedEvents.Clear();

        Assert.Equal(1, batch.Count);
    }

    private static ReceivedEvent CreateReceivedEvent(string streamId)
    {
        var eventInfo = new EventInfo(streamId, Guid.NewGuid(), 1, "EventType1", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, new Dictionary<string, string>());
        return new ReceivedEvent(new object(), eventInfo);
    }
}
