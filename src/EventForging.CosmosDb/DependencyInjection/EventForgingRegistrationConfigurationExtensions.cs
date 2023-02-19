using EventForging.CosmosDb.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.CosmosDb;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseCosmosDb(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingCosmosDbConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var cfgDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventForgingCosmosDbConfiguration));
        if (cfgDescriptor != null)
        {
            throw new EventForgingConfigurationException("CosmosDb already used.");
        }

        var configuration = new EventForgingCosmosDbConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IEventForgingCosmosDbConfiguration>(configuration);

        services.AddSingleton<ICosmosDbProvider, CosmosDbProvider>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IJsonSerializerOptionsProvider, CosmosJsonSerializerOptionsProvider>();
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
