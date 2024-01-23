using EventForging.EventStore.EventHandling;
using EventForging.EventStore.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.EventStore;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseEventStore(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventStoreEventForgingConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;

        var cfgDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventStoreEventForgingConfiguration));
        if (cfgDescriptor != null)
        {
            throw new EventForgingConfigurationException("EventStore already used.");
        }

        var configuration = new EventStoreEventForgingConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IEventStoreEventForgingConfiguration>(configuration);

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IJsonSerializerOptionsProvider, EventStoreJsonSerializerOptionsProvider>();
        services.AddSingleton(configuration.StreamIdFactory);

        services.AddEventStoreClient(ess =>
        {
            ess.ConnectivitySettings.Address = new Uri(configuration.Address!);
        });
        services.AddEventStorePersistentSubscriptionsClient(ess =>
        {
            ess.ConnectivitySettings.Address = new Uri(configuration.Address!);
        });

        services.AddSingleton<IEventDatabase, EventStoreEventDatabase>();

        services.AddSingleton<IEventsSubscriber, EventsSubscriber>();

        services.AddHostedService<EventStoreEventForgingHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(EventStoreEventForgingConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.Address))
        {
            throw new EventForgingConfigurationException("EventStore address cannot be empty.");
        }
    }
}
