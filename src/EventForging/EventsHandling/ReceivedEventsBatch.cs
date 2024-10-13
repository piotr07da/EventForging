using System.Collections;

namespace EventForging.EventsHandling;

public sealed class ReceivedEventsBatch : IEnumerable<ReceivedEvent>
{
    private readonly IReadOnlyList<ReceivedEvent> _items;
    
    public ReceivedEventsBatch(IReadOnlyList<ReceivedEvent> items)
    {
        _items = items;
    }
    
    public int Count => _items.Count;
    
    public IEnumerator<ReceivedEvent> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
