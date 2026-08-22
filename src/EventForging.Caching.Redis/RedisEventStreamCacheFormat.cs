using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using EventForging.Serialization;
using StackExchange.Redis;

namespace EventForging.Caching.Redis;

internal static class RedisEventStreamCacheFormat
{
    internal static readonly RedisValue LastCachedEventVersionField = "last-cached-event-version";

    internal static RedisKey CreateKey(
        Type aggregateType,
        string aggregateId,
        IRedisEventStreamCacheConfiguration configuration)
    {
        var compression = configuration.CompressionEnabled ? "gzip" : "raw";
        var aggregateAssemblyName = aggregateType.Assembly.GetName().Name;
        var aggregateTypeName = aggregateType.FullName ?? aggregateType.Name;
        return $"{configuration.KeyPrefix}{configuration.EventsPerChunk}:{compression}:"
               + $"{aggregateAssemblyName}:{aggregateTypeName}:{aggregateId}";
    }

    internal static long GetChunkStart(long eventVersion, int eventsPerChunk)
    {
        return eventVersion / eventsPerChunk * eventsPerChunk;
    }

    internal static RedisValue CreateChunkFieldName(long chunkStart)
    {
        return $"chunk:{chunkStart.ToString(CultureInfo.InvariantCulture)}";
    }

    internal static byte[] SerializeChunk(IReadOnlyCollection<SerializedEvent> events, bool compressionEnabled)
    {
        if (!compressionEnabled)
        {
            return JsonSerializer.SerializeToUtf8Bytes(events);
        }

        using var serializedChunk = new MemoryStream();
        using (var compression = new GZipStream(serializedChunk, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(compression, events);
        }

        return serializedChunk.ToArray();
    }

    internal static SerializedEvent[] DeserializeChunk(byte[] serializedChunk, bool compressionEnabled)
    {
        if (!compressionEnabled)
        {
            return JsonSerializer.Deserialize<SerializedEvent[]>(serializedChunk)!;
        }

        using var chunk = new MemoryStream(serializedChunk, false);
        using var decompression = new GZipStream(chunk, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<SerializedEvent[]>(decompression)!;
    }
}
