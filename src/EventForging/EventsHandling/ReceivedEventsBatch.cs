using System.Collections;

namespace EventForging.EventsHandling;

public sealed class ReceivedEventsBatch : IEnumerable<ReceivedEvent>
{
    private readonly IReadOnlyList<ReceivedEvent> _items;

    [Obsolete("Use ReceivedEventsBatch(string streamId, IReadOnlyList<ReceivedEvent> items) instead.")]
    public ReceivedEventsBatch(IReadOnlyList<ReceivedEvent> items)
        : this(GetFirstEventStreamId(items), items)
    {
    }

    public ReceivedEventsBatch(string streamId, IReadOnlyList<ReceivedEvent> items)
    {
        var itemsSnapshot = items.ToArray();

        if (itemsSnapshot.Length == 0)
        {
            throw new ArgumentException("Received events batch cannot be empty.", nameof(items));
        }

        if (itemsSnapshot.Any(item => item.EventInfo.StreamId != streamId))
        {
            throw new ArgumentException($"All events in the batch must belong to stream '{streamId}'.", nameof(items));
        }

        StreamId = streamId;
        _items = itemsSnapshot;
    }

    public string StreamId { get; }

    public int Count => _items.Count;

    public IEnumerator<ReceivedEvent> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static string GetFirstEventStreamId(IReadOnlyList<ReceivedEvent> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("Received events batch cannot be empty.", nameof(items));
        }

        return items[0].EventInfo.StreamId;
    }
}
