using System.Text.Json;

namespace EventForging.Serialization;

public interface IJsonSerializerOptionsProvider
{
    JsonSerializerOptions Get();
}
