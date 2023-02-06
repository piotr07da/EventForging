using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb.Serialization;

internal sealed class EventForgingCosmosSerializer : CosmosSerializer
{
    private readonly IEventSerializer _eventSerializer;
    private readonly ISerializerOptionsProvider _serializerOptionsProvider;

    public EventForgingCosmosSerializer(IEventSerializer eventSerializer, ISerializerOptionsProvider serializerOptionsProvider)
    {
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public override T FromStream<T>(Stream stream)
    {
        var sr = new StreamReader(stream);
        var json = sr.ReadToEnd();
        stream.Dispose();
        var o = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);

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
                var eventDataAsString = ed.Data.ToString();
                var eventMetadataAsString = ed.Metadata.ToString();

                var (eventData, eventMetadata) = _eventSerializer.DeserializeFromString(ed.EventType, eventDataAsString, eventMetadataAsString);
                ed.Data = eventData;
                ed.Metadata = eventMetadata;
            }
        }

        return o;
    }

    public override Stream ToStream<T>(T input)
    {
        const string dataReplaceTag = "REPLACE_WITH_DATA_SERIALIZED_USING_EVENT_FORGING_EVENTS_SERIALIZER";
        const string metadataReplaceTag = "REPLACE_WITH_METADATA_SERIALIZED_USING_EVENT_FORGING_EVENTS_SERIALIZER";

        var replaceNeeded = false;
        var serializedEventData = null as string;
        var serializedEventMetadata = null as string;
        if (input is EventDocument ed)
        {
            (var eventTypeName, serializedEventData, serializedEventMetadata) = _eventSerializer.SerializeToString(ed.Data, (ed.Metadata as EventMetadata)!);

            ed = ed.Clone();
            ed.Data = dataReplaceTag;
            ed.Metadata = metadataReplaceTag;
            ed.EventType = eventTypeName;
            input = (T)(object)ed;

            replaceNeeded = true;
        }

        var json = JsonSerializer.Serialize(input, JsonSerializerOptions);

        if (replaceNeeded)
        {
            json = json.Replace($"\"{dataReplaceTag}\"", serializedEventData);
            json = json.Replace($"\"{metadataReplaceTag}\"", serializedEventMetadata);
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
