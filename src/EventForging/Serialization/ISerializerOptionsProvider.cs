using System.Text.Json;

namespace EventForging.Serialization;

public interface ISerializerOptionsProvider
{
    JsonSerializerOptions Get();
}
