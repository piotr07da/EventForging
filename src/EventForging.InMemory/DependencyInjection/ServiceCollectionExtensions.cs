using System;
using System.Linq;
using EventForging.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.InMemory.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IEventForgingRegistrationConfiguration UseInMemory(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingInMemoryConfiguration> configurator = null)
    {
        var services = registrationConfiguration.Services;

        var eventDatabaseServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventDatabase));
        if (eventDatabaseServiceDescriptor != null)
        {
            throw new EventForgingConfigurationException($"Another type of event database has already been used: {eventDatabaseServiceDescriptor.ImplementationType?.Name}.");
        }

        configurator ??= ConfigureDefault;
        var configuration = new EventForgingInMemoryConfiguration();
        configurator(configuration);
        services.AddSingleton<IEventForgingInMemoryConfiguration>(configuration);

        services.AddTransient<IEventDatabase, InMemoryEventDatabase>();

        return registrationConfiguration;
    }

    private static void ConfigureDefault(IEventForgingInMemoryConfiguration configuration)
    {
        configuration.SerializationEnabled = false;
    }
}
