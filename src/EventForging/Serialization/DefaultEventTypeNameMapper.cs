using System;
using System.Reflection;

namespace EventForging.Serialization;

public class DefaultEventTypeNameMapper : IEventTypeNameMapper
{
    private readonly Assembly[] _assemblies;

    public DefaultEventTypeNameMapper(params Assembly[] assemblies)
    {
        _assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
    }

    public Type? TryGetType(string eventName)
    {
        foreach (var assembly in _assemblies)
        {
            var t = assembly.GetType(eventName);
            if (t != null)
            {
                return t;
            }
        }

        throw new EventForgingException(@$"Event CLR type for event '{eventName}' not found in specified assemblies.");
    }

    public string? TryGetName(Type eventType)
    {
        return eventType.FullName;
    }
}
