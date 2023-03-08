using EventForging.CosmosDb.EventHandling;
using EventForging.CosmosDb.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.CosmosDb;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseCosmosDb(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<ICosmosDbEventForgingConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var cfgDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICosmosDbEventForgingConfiguration));
        if (cfgDescriptor != null)
        {
            throw new EventForgingConfigurationException("CosmosDb already used.");
        }

        var configuration = new CosmosDbEventForgingConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<ICosmosDbEventForgingConfiguration>(configuration);

        services.AddSingleton<ICosmosDbProvider, CosmosDbProvider>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IJsonSerializerOptionsProvider, CosmosJsonSerializerOptionsProvider>();
        services.AddSingleton(configuration.StreamNameFactory);

        services.AddSingleton<IEventDatabase, CosmosDbEventDatabase>();

        services.AddSingleton<IEventsSubscriber, EventsSubscriber>();

        services.AddHostedService<CosmosDbEventForgingHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(CosmosDbEventForgingConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new EventForgingConfigurationException("Connection string must be defined.");
        }
    }
}
