using System.Text;
using System.Text.Json;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb.Serialization;

internal sealed class EventForgingCosmosSerializer : CosmosSerializer
{
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;
    private readonly ILogger _logger;

    public EventForgingCosmosSerializer(IEventSerializer eventSerializer, IJsonSerializerOptionsProvider serializerOptionsProvider, ILogger logger)
    {
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public override T FromStream<T>(Stream stream)
    {
        try
        {
            var sr = new StreamReader(stream);
            var json = sr.ReadToEnd();
            stream.Dispose();
            var o = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions)!;

            var eds = new List<EventDocument>();

            if (o is EventDocument oed)
            {
                eds.Add(oed);
            }
            else if (o is EventDocument[] oeds)
            {
                eds.AddRange(oeds);
            }

            foreach (var ed in eds)
            {
                if (ed.DocumentType == DocumentType.Event)
                {
                    if (ed.Data is null || ed.EventType is null)
                    {
                        throw new EventForgingException($"Data and type of event retrieved from the database cannot be null. StreamId is '{ed.StreamId}', Id is {ed.Id}.");
                    }

                    var eventDataAsString = ed.Data.ToString()!;

                    var eventData = _eventSerializer.DeserializeFromString(ed.EventType, eventDataAsString);
                    ed.Data = eventData;
                }
            }

            return o;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        const string dataReplaceTag = "REPLACE_WITH_DATA_SERIALIZED_USING_EVENT_FORGING_EVENTS_SERIALIZER";

        try
        {
            var replaceNeeded = false;
            var serializedEventData = null as string;
            if (input is EventDocument ed)
            {
                if (ed.Data is null)
                {
                    throw new EventForgingException($"Data of event written to the database cannot null. StreamId is '{ed.StreamId}', Id is {ed.Id}.");
                }

                serializedEventData = _eventSerializer.SerializeToString(ed.Data, out var eventTypeName);

                ed = ed.Clone();
                ed.Data = dataReplaceTag;
                ed.EventType = eventTypeName;
                input = (T)(object)ed;

                replaceNeeded = true;
            }

            var json = JsonSerializer.Serialize(input, JsonSerializerOptions);

            if (replaceNeeded)
            {
                json = json.Replace($"\"{dataReplaceTag}\"", serializedEventData);
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
}
