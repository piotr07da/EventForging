using System;
using EventStore.Client;

namespace EventForging.EventStore;

internal interface IEventStoreClientProvider
{
    void Initialize();
    void Dispose();
    EventStoreClient GetClient();
}

internal class EventStoreClientProvider : IEventStoreClientProvider
{
    private static EventStoreClient _client;

    private readonly IEventForgingEventStoreConfiguration _configuration;

    public EventStoreClientProvider(IEventForgingEventStoreConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void Initialize()
    {
        var settings = EventStoreClientSettings.Create(_configuration.ConnectionString);
        _client = new EventStoreClient(settings);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public EventStoreClient GetClient()
    {
        return _client;
    }
}
