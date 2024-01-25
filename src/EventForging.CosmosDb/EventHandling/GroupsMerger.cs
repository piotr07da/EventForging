namespace EventForging.CosmosDb.EventHandling;

public sealed class GroupsMerger<TGroup, TGroupItem>
{
    private readonly Func<TGroup, object> _groupKeyGetter;
    private readonly Func<TGroup, IEnumerable<TGroupItem>> _groupItemsGetter;

    public GroupsMerger(Func<TGroup, object> groupKeyGetter, Func<TGroup, IEnumerable<TGroupItem>> groupItemsGetter)
    {
        _groupKeyGetter = groupKeyGetter;
        _groupItemsGetter = groupItemsGetter;
    }

    public IEnumerable<IReadOnlyList<TGroupItem>> Merge(IEnumerable<TGroup> groups)
    {
        var groupsByKeys = groups.GroupBy(_groupKeyGetter).ToArray();
        foreach (var groupByKey in groupsByKeys)
        {
            var groupItems = groupByKey.SelectMany(_groupItemsGetter).ToArray();
            yield return groupItems;
        }
    }
}
