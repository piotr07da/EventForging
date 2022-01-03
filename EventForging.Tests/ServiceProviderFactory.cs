using EventForging.DependencyInjection;
using EventForging.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.Tests;

public static class ServiceProviderFactory
{
    public static IServiceProvider Create()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventForging();
        serviceCollection.AddEventForgingInMemory();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        return serviceProvider;
    }
}
