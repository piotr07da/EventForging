namespace EventForging.Caching;

internal sealed class EventStreamCacheReadException : Exception
{
    public EventStreamCacheReadException(string message)
        : base(message)
    {
    }

    public EventStreamCacheReadException(Exception innerException)
        : base("Reading events from the event stream cache failed.", innerException)
    {
    }
}
