using EventForging.DependencyInjection;
using EventForging.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.Tests;

public static class ServiceProviderFactory
{
    public static IServiceProvider Create()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventForging(c =>
        {
            c.Configuration.IdempotencyEnabled = false;

            c.UseInMemory(imConfigurator =>
            {
                imConfigurator.SerializationEnabled = false;
            });
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();

        return serviceProvider;
    }
}
