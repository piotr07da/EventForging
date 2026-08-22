using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EventForging.Caching.Redis;

public static class EventForgingRegistrationConfigurationExtensions
{
    /// <summary>Uses the Redis event stream cache.</summary>
    public static IEventForgingRegistrationConfiguration UseRedisEventStreamCache(
        this IEventForgingRegistrationConfiguration registrationConfiguration,
        Action<IRedisEventStreamCacheConfiguration> configurator)
    {
        var services = registrationConfiguration.Services;
        var registeredConnectionMultiplexerCount = services.Count(d => d.ServiceType == typeof(IConnectionMultiplexer));
        if (registeredConnectionMultiplexerCount > 1)
        {
            throw new EventForgingConfigurationException("Only one Redis IConnectionMultiplexer can be used by the event stream cache.");
        }

        var configuration = new RedisEventStreamCacheConfiguration();
        configurator(configuration);
        configuration.Validate(registeredConnectionMultiplexerCount == 1);

        services.AddSingleton<IRedisEventStreamCacheConfiguration>(configuration);
        if (registeredConnectionMultiplexerCount == 0)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(configuration.ConnectionString);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });
        }

        services.AddSingleton<IEventStreamCacheSessionFactory, RedisEventStreamCacheSessionFactory>();
        services.AddSingleton<IEventStreamCacheInvalidator, RedisEventStreamCacheInvalidator>();
        return registrationConfiguration;
    }
}
