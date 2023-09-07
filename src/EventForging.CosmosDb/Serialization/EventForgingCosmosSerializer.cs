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
        var t = typeof(T);
        if (t == typeof(HeaderDocument) ||
            t == typeof(EventDocument) ||
            t == typeof(EventsPacketDocument) ||
            t == typeof(HeaderDocument[]) ||
            t == typeof(EventDocument[]) ||
            t == typeof(EventsPacketDocument[]))
        {
            throw new EventForgingException($"Read documents only through {nameof(MasterDocument)} type.");
        }

        var sr = new StreamReader(stream);
        var json = sr.ReadToEnd();
        stream.Dispose();

        if (t != typeof(MasterDocument) && t != typeof(MasterDocument[]))
        {
            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions)!;
        }

        try
        {
            var jsonDoc = JsonDocument.Parse(json);
            var rootElement = jsonDoc.RootElement;

            var docElements = new List<JsonElement>();
            MasterDocument[] result;
            bool resultIsSingleElement;

            if (rootElement.ValueKind != JsonValueKind.Array)
            {
                docElements.Add(rootElement);
                result = new MasterDocument[1];
                resultIsSingleElement = true;
            }
            else
            {
                docElements.AddRange(rootElement.EnumerateArray());
                result = new MasterDocument[docElements.Count];
                resultIsSingleElement = false;
            }

            for (var dIx = 0; dIx < docElements.Count; ++dIx)
            {
                var element = docElements[dIx];

                var jsonDocumentType = element.EnumerateObject().First(p => p.Name.Equals("documentType", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
                var documentType = (DocumentType)Enum.Parse(typeof(DocumentType), jsonDocumentType, false);
                var documentJson = element.ToString();

                switch (documentType)
                {
                    default:
                    case DocumentType.Undefined:
                        result[dIx] = new MasterDocument { DocumentType = DocumentType.Undefined, };
                        break;

                    case DocumentType.Header:
                    {
                        var headerDocument = JsonSerializer.Deserialize<HeaderDocument>(documentJson, JsonSerializerOptions);
                        result[dIx] = new MasterDocument
                        {
                            DocumentType = DocumentType.Header,
                            HeaderDocument = headerDocument,
                        };
                        break;
                    }

                    case DocumentType.Event:
                    {
                        var eventDocument = JsonSerializer.Deserialize<EventDocument>(documentJson, JsonSerializerOptions) ?? throw new EventForgingException($"Document of type {nameof(DocumentType.Event)} deserialized to null.");
                        if (eventDocument.Data is null || eventDocument.EventType is null)
                        {
                            throw new EventForgingException($"Data and type of event retrieved from the database cannot be null. StreamId is '{eventDocument.StreamId}', Id is {eventDocument.Id}.");
                        }

                        var eventDataAsString = eventDocument.Data.ToString()!;
                        var eventData = _eventSerializer.DeserializeFromString(eventDocument.EventType, eventDataAsString);
                        eventDocument.Data = eventData;

                        result[dIx] = new MasterDocument
                        {
                            DocumentType = DocumentType.Event,
                            EventDocument = eventDocument,
                        };

                        break;
                    }

                    case DocumentType.EventsPacket:
                    {
                        var eventsPacketDocument = JsonSerializer.Deserialize<EventsPacketDocument>(documentJson, JsonSerializerOptions) ?? throw new EventForgingException($"Document of type {nameof(DocumentType.EventsPacket)} deserialized to null.");
                        var nullDataOrEventTypeEvent = eventsPacketDocument.Events.FirstOrDefault(e => e.Data is null || e.EventType is null);
                        if (nullDataOrEventTypeEvent is not null)
                        {
                            throw new EventForgingException($"Data and type of event retrieved from the database cannot be null. StreamId is '{eventsPacketDocument.StreamId}', Id is {nullDataOrEventTypeEvent.EventId}.");
                        }

                        foreach (var e in eventsPacketDocument.Events)
                        {
                            var eventDataAsString = e.Data!.ToString()!;
                            var eventData = _eventSerializer.DeserializeFromString(e.EventType!, eventDataAsString);
                            e.Data = eventData;
                        }

                        result[dIx] = new MasterDocument
                        {
                            DocumentType = DocumentType.EventsPacket,
                            EventsPacketDocument = eventsPacketDocument,
                        };

                        break;
                    }
                }
            }

            return resultIsSingleElement ? (T)(object)result[0] : (T)(object)result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        const string dataReplaceTagPrefix = "REPLACE_WITH_DATA_SERIALIZED_USING_EVENT_FORGING_EVENTS_SERIALIZER_";

        try
        {
            var serializedEventDatas = new List<string>();

            if (input is EventDocument ed)
            {
                if (ed.Data is null)
                    throw new EventForgingException($"Data of event written to the database cannot null. StreamId is '{ed.StreamId}', Id is {ed.Id}.");

                serializedEventDatas.Add(_eventSerializer.SerializeToString(ed.Data, out var eventTypeName));

                ed = ed.Clone();
                ed.Data = $"{dataReplaceTagPrefix}0";
                ed.EventType = eventTypeName;
                input = (T)(object)ed;
            }
            else if (input is EventsPacketDocument epd)
            {
                var nullDataEvent = epd.Events.FirstOrDefault(e => e.Data is null);
                if (nullDataEvent is not null)
                    throw new EventForgingException($"Data of event written to the database cannot null. StreamId is '{epd.StreamId}', Id is {nullDataEvent.EventId}.");

                var eventTypeNames = new List<string>();
                foreach (var e in epd.Events)
                {
                    serializedEventDatas.Add(_eventSerializer.SerializeToString(e.Data!, out var eventTypeName));
                    eventTypeNames.Add(eventTypeName);
                }

                epd = epd.Clone();
                for (var eIx = 0; eIx < epd.Events.Count; ++eIx)
                {
                    var e = epd.Events[eIx];
                    e.Data = $"{dataReplaceTagPrefix}{eIx}";
                    e.EventType = eventTypeNames[eIx];
                }

                input = (T)(object)epd;
            }

            var json = JsonSerializer.Serialize(input, JsonSerializerOptions);

            for (var sedIx = 0; sedIx < serializedEventDatas.Count; ++sedIx)
            {
                var serializedEventData = serializedEventDatas[sedIx];
                var dataReplaceTag = $"{dataReplaceTagPrefix}{sedIx}";
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
