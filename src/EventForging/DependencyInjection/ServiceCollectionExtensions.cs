using EventForging.Diagnostics.Logging;
using EventForging.EventsHandling;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventForging(this IServiceCollection services, Action<IEventForgingRegistrationConfiguration> configurator)
    {
        if (services.Any(d => d.ServiceType == typeof(IEventForgingConfiguration)))
        {
            throw new EventForgingConfigurationException("EventForging has already been added.");
        }

        var configuration = new EventForgingConfiguration();
        var registrationConfiguration = new EventForgingRegistrationConfiguration(services, configuration);
        configurator(registrationConfiguration);

        services.AddSingleton(typeof(IEventForgingConfiguration), configuration);
        services.AddSingleton(typeof(IEventForgingSerializationConfiguration), configuration.Serialization);
        services.AddTransient(typeof(IRepository<>), typeof(Repository<>));

        services.AddSingleton<IEventDispatcher, EventDispatcher>();

        services.AddSingleton<IEventForgingLoggerProvider, EventForgingLoggerProvider>();

        EventForgingStaticConfigurationProvider.ApplyMethodsRequiredForAllAppliedEvents = configuration.ApplyMethodsRequiredForAllAppliedEvents;

        return services;
    }
}
