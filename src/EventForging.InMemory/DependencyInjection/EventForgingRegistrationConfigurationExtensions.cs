using EventForging.InMemory.EventHandling;
using EventForging.InMemory.Serialization;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging.InMemory;

public static class EventForgingRegistrationConfigurationExtensions
{
    public static IEventForgingRegistrationConfiguration UseInMemory(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IInMemoryEventForgingConfiguration>? configurator = null)
    {
        var services = registrationConfiguration.Services;

        var eventDatabaseServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventDatabase));
        if (eventDatabaseServiceDescriptor != null)
        {
            throw new EventForgingConfigurationException($"Another type of event database has already been used: {eventDatabaseServiceDescriptor.ImplementationType?.Name}.");
        }

        configurator ??= ConfigureDefault;
        var configuration = new InMemoryEventForgingConfiguration();
        configurator(configuration);
        services.AddSingleton<IInMemoryEventForgingConfiguration>(configuration);

        if (configuration.SerializationEnabled)
        {
            services.AddSingleton<IEventSerializer, JsonEventSerializer>();
            services.AddSingleton<IJsonSerializerOptionsProvider, InMemoryJsonSerializerOptionsProvider>();
        }
        else
        {
            services.AddSingleton<IEventSerializer, DummyEventSerializer>();
        }

        services.AddSingleton(configuration.StreamNameFactory);

        services.AddSingleton<IEventDatabase, InMemoryEventDatabase>();

        services.AddSingleton<ISubscriptions, Subscriptions>();

        services.AddHostedService<InMemoryEventForgingHostedService>();

        return registrationConfiguration;
    }

    private static void ConfigureDefault(IInMemoryEventForgingConfiguration configuration)
    {
        configuration.SerializationEnabled = false;
    }
}
