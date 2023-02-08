using EventStore.Client;

namespace EventForging.EventStore;

internal interface IEventStoreClientProvider
{
    void Initialize();
    void Dispose();
    EventStoreClient GetClient();
}
