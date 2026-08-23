using Microsoft.Extensions.DependencyInjection;

namespace EventForging.Caching.Memory;

public static class EventForgingRegistrationConfigurationExtensions
{
    /// <summary>Uses the in-process memory event stream cache.</summary>
    public static IEventForgingRegistrationConfiguration UseMemoryEventStreamCache(
        this IEventForgingRegistrationConfiguration registrationConfiguration,
        Action<IMemoryEventStreamCacheConfiguration>? configurator = null)
    {
        var services = registrationConfiguration.Services;
        var configuration = new MemoryEventStreamCacheConfiguration();
        configurator?.Invoke(configuration);
        configuration.Validate();

        services.AddSingleton<IEventStreamCacheConfiguration>(configuration);
        services.AddSingleton<IMemoryEventStreamCacheConfiguration>(configuration);
        services.AddSingleton<MemoryEventStreamCache>();
        services.AddSingleton<IEventStreamCacheSessionFactory, MemoryEventStreamCacheSessionFactory>();
        services.AddSingleton<IEventStreamCacheInvalidator, MemoryEventStreamCacheInvalidator>();
        return registrationConfiguration;
    }
}
