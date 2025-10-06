using EventForging.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.Tests;

public static class ServiceProviderFactory
{
    public static IServiceProvider Create()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventForging(r =>
        {
            r.ConfigureEventForging(c =>
            {
                c.IdempotencyEnabled = false;
            });

            r.AddRepositorySaveInterceptor<GenericRepositorySaveInterceptor>();
            r.AddRepositorySaveInterceptor<SpecificRepositorySaveInterceptor, BreweryAggregate>();

            r.UseInMemory(imConfigurator =>
            {
                imConfigurator.SerializationEnabled = false;
            });
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();

        return serviceProvider;
    }
}
