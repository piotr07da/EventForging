using System;
using System.Linq;
using EventForging.DependencyInjection;
using EventForging.EventStore.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.EventStore.DependencyInjection;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseEventStore(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingEventStoreConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var cfgDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventForgingEventStoreConfiguration));
        if (cfgDescriptor != null)
        {
            throw new EventForgingConfigurationException("EventStore already used.");
        }

        var configuration = new EventForgingEventStoreConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IEventForgingEventStoreConfiguration>(configuration);
        services.AddSingleton<IEventStoreClientProvider, EventStoreClientProvider>();
        services.AddSingleton<IStreamNameFactory, DefaultStreamNameFactory>();
        services.AddSingleton<IJsonSerializerOptionsProvider, EventStoreJsonSerializerOptionsProvider>();

        services.AddTransient<IEventDatabase, EventStoreEventDatabase>();

        services.AddHostedService<EventForgingEventStoreHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(EventForgingEventStoreConfiguration configuration)
    {
    }
}
