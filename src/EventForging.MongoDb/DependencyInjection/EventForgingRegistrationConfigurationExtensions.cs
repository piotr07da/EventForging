using EventForging.MongoDb;
using EventForging.MongoDb.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.EventStore;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseMongoDb(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IMongoDbEventForgingConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var cfgDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMongoDbEventForgingConfiguration));
        if (cfgDescriptor != null)
        {
            throw new EventForgingConfigurationException("MongoDb already used.");
        }

        var configuration = new MongoDbEventForgingConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IMongoDbEventForgingConfiguration>(configuration);

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IJsonSerializerOptionsProvider, MongoDbJsonSerializerOptionsProvider>();

        services.AddSingleton<IEventDatabase, MongoDbEventDatabase>();

        services.AddHostedService<MongoDbEventForgingHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(MongoDbEventForgingConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new EventForgingConfigurationException("MongoDb connection string cannot be empty.");
        }
    }
}
