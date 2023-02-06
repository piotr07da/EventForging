using System;
using System.Linq;
using EventForging.CosmosDb.Serialization;
using EventForging.DependencyInjection;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.CosmosDb.DependencyInjection;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseCosmosDb(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingCosmosDbConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var eventDatabaseServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventForgingCosmosDbConfiguration));
        if (eventDatabaseServiceDescriptor != null)
        {
            throw new EventForgingConfigurationException($"Another type of event database has already been used: {eventDatabaseServiceDescriptor.ImplementationType?.Name}.");
        }

        var configuration = new EventForgingCosmosDbConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IEventForgingCosmosDbConfiguration>(configuration);
        services.AddSingleton<ICosmosDbProvider, CosmosDbProvider>();
        services.AddSingleton<ISerializerOptionsProvider, EventForgingCosmosSerializerOptionsProvider>();
        services.AddSingleton<IStreamNameFactory, DefaultStreamNameFactory>();

        services.AddTransient<IEventDatabase, CosmosDbEventDatabase>();

        services.AddHostedService<EventForgingCosmosDbHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(EventForgingCosmosDbConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new EventForgingConfigurationException("Connection string must be defined.");
        }
    }
}
