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
        services.AddSingleton(configuration.StreamIdFactory);

        services.AddSingleton<CosmosDbEventDatabase>();
        services.AddSingleton<IEventDatabase>(sp => sp.GetRequiredService<CosmosDbEventDatabase>());
        services.AddSingleton<IDestructiveEventDatabase>(sp => sp.GetRequiredService<CosmosDbEventDatabase>());

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

        foreach (var subscription in configuration.Subscriptions)
        {
            var hasMatchingAggregateLocation = configuration.AggregateLocations.Values.Any(location =>
                location.DatabaseName == subscription.DatabaseName &&
                location.EventsContainerName == subscription.EventsContainerName);

            if (!hasMatchingAggregateLocation)
            {
                throw new EventForgingConfigurationException($"Cannot add event subscription '{subscription.SubscriptionName}' for [{subscription.DatabaseName}, {subscription.EventsContainerName}]. Register aggregate location for this database and events container first using {nameof(ICosmosDbEventForgingConfiguration)}.{nameof(ICosmosDbEventForgingConfiguration.AddAggregateLocations)}.");
            }
        }
    }
}
