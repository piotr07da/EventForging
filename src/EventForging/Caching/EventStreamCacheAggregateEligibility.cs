namespace EventForging.Caching;

internal static class EventStreamCacheAggregateEligibility
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const double HashValueCount = (double)uint.MaxValue + 1d;

    public static bool IsEligible(string aggregateId, double aggregateCachingRatio)
    {
        var threshold = (ulong)(aggregateCachingRatio * HashValueCount);
        return CalculateHash(aggregateId) < threshold;
    }

    private static uint CalculateHash(string aggregateId)
    {
        var hash = FnvOffsetBasis;
        for (var characterIndex = 0; characterIndex < aggregateId.Length; ++characterIndex)
        {
            hash = unchecked((hash ^ aggregateId[characterIndex]) * FnvPrime);
        }

        return hash;
    }
}
