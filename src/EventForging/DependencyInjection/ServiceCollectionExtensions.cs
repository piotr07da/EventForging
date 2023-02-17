﻿using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

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

        return services;
    }
}
