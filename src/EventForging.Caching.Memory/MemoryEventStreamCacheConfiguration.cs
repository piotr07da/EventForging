namespace EventForging.Caching.Memory;

internal sealed class MemoryEventStreamCacheConfiguration : IMemoryEventStreamCacheConfiguration
{
    internal const double DefaultAggregateCachingRatio = 1d;
    internal const int DefaultMinimumEventCount = 3000;
    internal const int DefaultMaximumCachedStreamCount = 1000;
    internal const int DefaultMaximumTotalCachedEventCount = 200_000;
    internal static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromSeconds(60);

    public double AggregateCachingRatio { get; set; } = DefaultAggregateCachingRatio;
    public int MinimumEventCount { get; set; } = DefaultMinimumEventCount;
    public TimeSpan SlidingExpiration { get; set; } = DefaultSlidingExpiration;
    public int MaximumCachedStreamCount { get; set; } = DefaultMaximumCachedStreamCount;
    public int MaximumTotalCachedEventCount { get; set; } = DefaultMaximumTotalCachedEventCount;

    internal void Validate()
    {
        if (MinimumEventCount <= 0)
        {
            throw new EventForgingConfigurationException($"{nameof(MinimumEventCount)} must be greater than zero.");
        }

        if (SlidingExpiration <= TimeSpan.Zero)
        {
            throw new EventForgingConfigurationException($"{nameof(SlidingExpiration)} must be greater than zero.");
        }

        if (MaximumCachedStreamCount <= 0)
        {
            throw new EventForgingConfigurationException($"{nameof(MaximumCachedStreamCount)} must be greater than zero.");
        }

        if (MaximumTotalCachedEventCount <= 0)
        {
            throw new EventForgingConfigurationException($"{nameof(MaximumTotalCachedEventCount)} must be greater than zero.");
        }
    }
}
