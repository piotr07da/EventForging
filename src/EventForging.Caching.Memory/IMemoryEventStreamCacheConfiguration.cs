namespace EventForging.Caching.Memory;

/// <summary>Configures the in-process memory event stream cache.</summary>
public interface IMemoryEventStreamCacheConfiguration : IEventStreamCacheConfiguration
{
    /// <summary>The maximum number of event streams cached across all aggregate types.</summary>
    int MaximumCachedStreamCount { get; set; }

    /// <summary>The maximum total number of events cached across all event streams and aggregate types.</summary>
    int MaximumTotalCachedEventCount { get; set; }
}
