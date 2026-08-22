using EventForging.Diagnostics.Logging;
using EventForging.Caching;
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

        var configuration = new EventForgingConfiguration(new EventForgingSerializationConfiguration());
        var registrationConfiguration = new EventForgingRegistrationConfiguration(services, configuration);
        configurator(registrationConfiguration);
        var cacheSessionFactoryCount = services.Count(
            descriptor => descriptor.ServiceType == typeof(IEventStreamCacheSessionFactory));
        var cacheInvalidatorCount = services.Count(
            descriptor => descriptor.ServiceType == typeof(IEventStreamCacheInvalidator));
        if (cacheSessionFactoryCount > 1 || cacheInvalidatorCount > 1)
        {
            throw new EventForgingConfigurationException("Only one event stream cache can be registered.");
        }

        if (cacheSessionFactoryCount != cacheInvalidatorCount)
        {
            throw new EventForgingConfigurationException(
                "An event stream cache session factory and invalidator must be registered together.");
        }

        services.AddSingleton(typeof(IEventForgingConfiguration), configuration);
        services.AddSingleton(typeof(IEventForgingSerializationConfiguration), configuration.Serialization);
        services.AddSingleton<EventStreamCacheSessionFactory>();
        services.AddSingleton<EventStreamCacheInvalidator>();
        services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));
        services.AddSingleton<IEventDispatcher, EventDispatcher>();
        services.AddSingleton<IEventForgingLoggerProvider, EventForgingLoggerProvider>();

        EventForgingStaticConfigurationProvider.ApplyMethodsRequiredForAllAppliedEvents = configuration.ApplyMethodsRequiredForAllAppliedEvents;

        return services;
    }
}
