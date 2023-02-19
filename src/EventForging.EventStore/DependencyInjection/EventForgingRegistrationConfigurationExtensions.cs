using EventForging.EventStore.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.EventStore;

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

        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<IJsonSerializerOptionsProvider, EventStoreJsonSerializerOptionsProvider>();
        services.AddSingleton<IStreamNameFactory, DefaultStreamNameFactory>();

        services.AddEventStoreClient(ess =>
        {
            ess.ConnectivitySettings.Address = new Uri(configuration.Address!);
        });

        services.AddTransient<IEventDatabase, EventStoreEventDatabase>();

        services.AddHostedService<EventForgingEventStoreHostedService>();

        return registrationConfiguration;
    }

    private static void ValidateConfiguration(EventForgingEventStoreConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.Address))
        {
            throw new EventForgingConfigurationException("EventStore address cannot be empty.");
        }
    }
}
