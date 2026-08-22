namespace EventForging.Caching.Redis;

internal sealed record SerializedEvent(string EventName, byte[] SerializedData);
