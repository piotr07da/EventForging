using EventForging.InMemory.EventHandling;
using EventForging.InMemory.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.InMemory;

public static class ServiceCollectionExtensions
{
    public static IEventForgingRegistrationConfiguration UseInMemory(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingInMemoryConfiguration>? configurator = null)
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

        if (configuration.SerializationEnabled)
        {
            services.AddSingleton<IEventSerializer, JsonEventSerializer>();
            services.AddSingleton<IJsonSerializerOptionsProvider, InMemoryJsonSerializerOptionsProvider>();
        }
        else
        {
            services.AddSingleton<IEventSerializer, DummyEventSerializer>();
        }

        services.AddSingleton<IEventDatabase, InMemoryEventDatabase>();

        services.AddSingleton<ISubscriptions, Subscriptions>();

        services.AddHostedService<InMemoryEventForgingHostedService>();

        return registrationConfiguration;
    }

    private static void ConfigureDefault(IEventForgingInMemoryConfiguration configuration)
    {
        configuration.SerializationEnabled = false;
    }
}
