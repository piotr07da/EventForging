namespace EventForging.CosmosDb;

public static class ListExtensions
{
    public static IReadOnlyList<IReadOnlyList<T>> SplitEvenly<T>(this IReadOnlyList<T> list, int targetAmountOfLists)
    {
        var sublistMinSize = list.Count / targetAmountOfLists;
        var remainder = list.Count % targetAmountOfLists;
        var result = new List<IReadOnlyList<T>>(targetAmountOfLists);
        var skipElementIx = 0;
        for (var slIx = 0; slIx < targetAmountOfLists; ++slIx)
        {
            var sublistSize = sublistMinSize + (slIx < remainder ? 1 : 0);
            result.Add(list.Skip(skipElementIx).Take(sublistSize).ToList());
            skipElementIx += sublistSize;
        }

        return result;
    }
}
