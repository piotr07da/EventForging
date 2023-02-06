using System;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static void ConfigureSerialization(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingSerializationConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var serializationConfiguration = new EventForgingSerializationConfiguration();
        configurator(serializationConfiguration);
        services.AddSingleton<IEventForgingSerializationConfiguration>(serializationConfiguration);

        services.AddTransient<IEventSerializer, JsonEventSerializer>();
    }
}
