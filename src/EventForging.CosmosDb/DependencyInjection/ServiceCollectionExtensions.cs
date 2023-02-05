using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventForging.CosmosDb.Serialization;
using EventForging.DependencyInjection;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventForging.CosmosDb.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IEventForgingRegistrationConfiguration UseCosmosDb(this IEventForgingRegistrationConfiguration registrationConfiguration, Action<IEventForgingCosmosDbConfiguration>? configurator = null)
    {
        var services = registrationConfiguration.Services;

        var eventDatabaseServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventForgingCosmosDbConfiguration));
        if (eventDatabaseServiceDescriptor != null)
        {
            throw new EventForgingConfigurationException($"Another type of event database has already been used: {eventDatabaseServiceDescriptor.ImplementationType?.Name}.");
        }

        configurator ??= ConfigureDefault;
        var configuration = new EventForgingCosmosDbConfiguration();
        configurator(configuration);
        ValidateConfiguration(configuration);
        services.AddSingleton<IEventForgingCosmosDbConfiguration>(configuration);
        services.AddSingleton<ICosmosDbProvider, CosmosDbProvider>();
        services.AddSingleton<ISerializerOptionsProvider, EventForgingCosmosSerializerOptionsProvider>();
        services.AddSingleton<IStreamNameFactory, DefaultStreamNameFactory>();

        services.AddTransient<IEventDatabase, CosmosDbEventDatabase>();

        services.AddHostedService<EventForgingCosmosDbHostedService>();

        return registrationConfiguration;
    }

    private static void ConfigureDefault(IEventForgingCosmosDbConfiguration configuration)
    {
    }

    private static void ValidateConfiguration(EventForgingCosmosDbConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new EventForgingConfigurationException("Connection string must be defined.");
        }
    }
}

internal sealed class EventForgingCosmosDbHostedService : IHostedService, IAsyncDisposable
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private bool _stopRequested;

    public EventForgingCosmosDbHostedService(
        ICosmosDbProvider cosmosDbProvider
    )
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosDbProvider.InitializeAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        await _cosmosDbProvider.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
