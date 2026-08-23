# EventForging

EventForging is a free, open-source, lightweight .NET framework for event sourced applications.

EventForging is MIT licensed.

## Status

[![build-n-publish](https://github.com/piotr07da/EventForging/actions/workflows/build-n-publish.yml/badge.svg)](https://github.com/piotr07da/EventForging/actions/workflows/build-n-publish.yml)

## Features

- InMemory database
- EventStore database integration
- CosmosDb database integration
- Event stream caching with memory and Redis implementations
- Opened for integration with other databases
- Per command idempotency - ensures that the same command does not get processed more than once
- Stores conversationId, messageId, and initiatorId for tracking and debugging purposes
- No inheritance hell - there are no base classes, you don't have to inherit from anything

## Using EventForging

### Application Layer

To get and save an aggregate from and to the database we use the `IRepository<TAggregate>` interface.

```csharp
private readonly IRepository<Customer> _repository;

public async Task Consume(ConsumeContext<RegisterCustomer> context)
{
    var command = context.Message;

    var customer = Customer.Register(CustomerId.FromValue(command.CustomerId), CustomerName.FromValue(command.Name));
    await _repository.SaveAsync(command.CustomerId, customer, ExpectedVersion.None, context.ConversationId, context.InitiatorId);
}

public async Task Consume(ConsumeContext<RenameCustomer> context)
{
    var command = context.Message;

    var customer = await _repository.GetAsync(command.CustomerId);
    customer.Rename(CustomerName.FromValue(command.Name));
    await _repository.SaveAsync(command.CustomerId, customer, ExpectedVersion.Retrieved, context.ConversationId, context.InitiatorId);
}
```

Lets explain the arguments of the `SaveAsync` method:

- `aggregateId` - the identifier of the aggregate. It can be either `Guid` or `string`.
- `aggregate` - the aggregate that we are saving to the repository.
- `expectedVersion` - the expected version of the aggregate in the repository.
    - Pass `ExpectedVersion.None` if you expect that the aggregate does not exist in the repository. This is the case
      for newly created aggregates.
    - Pass `ExpectedVersion.Any` if you do not want to check the version of the aggregate during save.
    - Pass `ExpectedVersion.Retrieved` if you want the version of the saved aggregate to match the version it had when
      it was retrieved from the repository. This is similar to Any, but ensures that the version of the aggregate does
      not change between retrieving and saving. It provides consistency for the operation executed on the aggregate.
    - Pass a number if you expect a specific version.
- `conversationId` - the ID of the conversation.
- `initiatorId` - the ID of of the initiator.

### Domain Layer

Every aggregate has to implement the `IEventForged` interface. The simplest form looks like this:

```csharp
public class Customer : IEventForged
{
    public Customer()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }
}
```

Let's add two methods - the first will be a factory method, and the second will be a method for renaming a customer.
For the simplicity of the example, there is no additional logic - just applying the events.

```csharp
public static Customer Register(CustomerId id, CustomerName name)
{
    var customer = new Customer();
    var events = customer.Events;
    events.Apply(new CustomerRegistered(id.Value));
    events.Apply(new CustomerNamed(id.Value, name.Value));
    return customer;
}

public void Rename(CustomerName name)
{
    Events.Apply(new CustomerNamed(Id.Value, name.Value));
}
```

The final step is to rebuild the current state of the aggregate by handling the newly applied events as well as events
loaded from the database.
This is achived by creating a private `Apply` method for each type of event that can occur.

```csharp
public CustomerId Id { get; private set; }
public CustomerName Name { get; private set; }

private void Apply(CustomerRegistered e)
{
    Id = CustomerId.Restore(e.Id);
}

private void Apply(CustomerNamed e)
{
    Name = CustomerName.Restore(e.Name);
}
```

## Idempotency

EventForging provides per command idempotency which ensures that the same command does not get processed more than once.
For that to work each command has to have unique identifier.
The command identifier must be provided to the `IRepository<TAggregate>.SaveAsync(...)` method as `initiatorId`
parameter.
This feature is enabled by default but can be disabled.
In case of disabling idempotency, the `Guid.Empty` can be passed as `initiatorId`.

### How it works

When the initiator identifier is passed and it is non-empty GUID, its value is used as a seed
to generate consecutive event identifiers in a deterministic way.
An event with a given identifier can be saved to the database only once.
The first attempt to save an event with a given identifier will result in the event being saved to the database,
and all subsequent attempts will be skipped.

## Event handling <a name="event-handling"></a>

If particular database integration supports event subscriptions, event handling feature can be used.
To use it, an `IEventHandler<T>` interface must be implemented as follows:

```csharp
public sealed class SomethingHappendHandler : IEventHandler<SomethingHappend>
{
    public string SubscriptionName => "TestSubscription";

    public async Task HandleAsync(SomethingHappend e, EventInfo ei, CancellationToken cancellationToken)
    {
        // your code goes here
    }
}
```

or an `IAllEventsHandler` interface must be implemented as follows:

```csharp
public sealed class AllEventsHandler : IAllEventsHandler
{
    public string SubscriptionName => "TestSubscription";

    public async Task HandleAsync(object e, EventInfo ei, CancellationToken cancellationToken)
    {
        // your code goes here
    }
}
```

Each database integration provides its own way to define which events will go to which subscription.
Please see the [Configuration](#configuration) section.

Intentionally, there is no built-in mechanism for dealing with exceptions.
By default, messages will be retried forever in case of an exception.
Therefore it is important to catch exceptions and avoid `HandleAsync` failing.

## Event stream cache

EventForging can cache events that have already been read from the database. When the aggregate is read again, cached
events are used and only events added later are loaded from the database. Without a registered cache provider, all events
are loaded from the database on every read.

### Common behavior

The cache is updated only after all events returned by the database have been read successfully. If the cache fails, the
aggregate is loaded entirely from the database. Event streams are append-only. A cached entry can be removed explicitly
through `IEventStreamCacheInvalidator.InvalidateAsync<TAggregate>`.

Cache metrics are available through `EventForgingDiagnosticsInfo.MeterName`; memory-provider size and removal metrics use
`EventForgingMemoryEventStreamCacheDiagnosticsInfo.MeterName`. Metrics are tagged with `ef.aggregate.type`; served events
can be split by `ef.event_stream.read.source`, and cache lookups by `ef.cache.lookup.result`.

### Memory

```csharp
services.AddEventForging(r =>
{
    r.UseMemoryEventStreamCache(c =>
    {
        c.MinimumEventCount = 3000;
        c.SlidingExpiration = TimeSpan.FromSeconds(60);
        c.MaximumCachedStreamCount = 1000;
        c.MaximumTotalCachedEventCount = 200_000;
    });
});
```

The `EventForging.Caching.Memory` provider applies its stream and event limits across all aggregate types and removes the
least recently used streams when a limit is exceeded. A stream larger than `MaximumTotalCachedEventCount` is not cached.

### Redis

Use `EventForging.Caching.Redis` when the cache must be shared by multiple application instances. Replace the memory
provider registration with:

```csharp
r.UseRedisEventStreamCache(c =>
{
    c.MinimumEventCount = 3000;
    c.SlidingExpiration = TimeSpan.FromSeconds(60);
    c.ConnectionString = "localhost:6379";
    c.KeyPrefix = "my-application:event-stream-cache:";
    c.EventsPerChunk = 1000;
    c.CompressionEnabled = true;
});
```

`KeyPrefix` is added before every Redis key and must be unique to the application and environment. An existing
`IConnectionMultiplexer` can be registered instead of a connection string. Redis stores events in groups of
`EventsPerChunk` and compresses them by default. Event type name mappers must be configured because the events are
serialized.

## Configuration <a name="configuration"></a>

To enable and configure EventForging we need to use `AddEventForging` method.

```csharp
services.AddEventForging(r =>
{
    r.ConfigureEventForging(efCfg => { });
    r.UseInMemory(dbCfg => { });
});
```

where:

- `r` is used to register EventForging components
- `efCfg` is used to configure global EventForging parameters (not specific to the type of the database used)
- `dbCfg` is used to configure used database (in this case InMemory), each database has its own configuration

### Global configuration

EventForging can be configured using following code:

```csharp
r.ConfigureEventForging(c =>
{
    c.IdempotencyEnabled = true;
    c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(_assembly));
});
```

#### Idempotency

The idempotency feature can be either enabled (which is the default) or disabled:

```csharp
c.IdempotencyEnabled = true;
```

#### Serialization

No matter which database is used, the mapping between the CLR type of the event
and the name of the event stored in the database must be configured.
This mapping enables EventForging to determine to which CLR type the serialized event needs to be deserialized.
EventForging provides a DefaultEventTypeNameMapper that maps the CLR type of an event to its full name (namespace +
class name).
Custom event type name mappers can be provided by implementing the IEventTypeNameMapper interface.

```csharp
c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(_assembly));
```

### InMemory

InMemory provides an in-memory database that can be used for development or testing purposes.
It can be configured using the following code:

```csharp
r.UseInMemory(c =>
{
});
```

#### Serialization

By default serialization is disabled when using InMemory mode, but it can be enabled.
When enabling serialization, at least one event type name mapper must be defined.

```csharp
c.SerializationEnabled = true;
```

#### Event handling

To subscribe to the event streams, subscriptions must be added.
All events will be directed to all matching implementations of `IEventHandler<TEvent>` or to the implementations
of `IAnyEventHandler`.
For more details, please see [Event Handling](#event-handling) section.

```csharp
c.AddEventSubscription("TestSubscription");
```

### EventStore

EventStore can be configured using following code:

```csharp
r.UseEventStore(c =>
{
});
```

#### Address

The address of the EventStore database is required and is specified as shown below:

```csharp
c.Address = configuration["EventStore:Address"];
```

#### Stream Id

By default, the stream id will be constructed from the name of the aggregate CLR type and its Id.
However, there is an option to provide a custom stream id factory by either providing a lambda expression
or by implementing the `IStreamIdFactory` interface.

```csharp
c.SetStreamIdFactory((aggregateType, aggregateId) => $"{EventsStreamIdPrefix}-{aggregateType.Name}-{aggregateId}");
c.SetStreamIdFactory(new MyCustomStreamIdFactory());
```

#### Event handling

To subscribe to the event streams, subscriptions must be added.
All events from specified stream and group will be directed to all matching implementations of `IEventHandler<TEvent>`
or to the implementations of `IAnyEventHandler`.
This feature is based on EventStore persistent subscriptions, please see
the [EventStore documentation](https://www.eventstore.com/).
The same subscription name can be used multiple times with different stream ids (aka stream names) and group names,
directing events from different streams to the same event handlers.
For more details, please see [Event Handling](#event-handling) section.

```csharp
cc.AddEventsSubscription("TestSubscription", "TestSubscriptionStreamId", "TestSubscriptionGroupName");
cc.AddEventsSubscription("TestSubscription", "TestSubscriptionStreamId", "TestSubscriptionGroupName", PersistentSubscriptionNakEventAction.Park);
```

The last parameter is used to specify how the exception that occured during event handling must be handled. It is
EventStore feature.

### CosmosDb

CosmosDb can be configured using following code:

```csharp
r.UseCosmosDb(c =>
{
});
```

#### Connection string

The connection string to the CosmosDb database ir required and is specified as shown below:

```csharp
c.ConnectionString = ConnectionString;
```

#### Aggregate locations

To be able to save an aggregate to the database, the aggragte location must be configured.
This configuration tells EventForging to which database and to which container events will be saved.
Here are few examples of how it can be configured:

```csharp
c.AddAggregateLocations("DatabaseName", "EventsContainerName", assembly);
c.AddAggregateLocations("DatabaseName", "EventsContainerName", assembly, t => true);
c.AddAggregateLocations("DatabaseName", "EventsContainerName", typeof(Aggregate1));
```

Aggregate locations and subscriptions can also be configured together:

```csharp
c.AddEventsContainerForAggregate("DatabaseName", "EventsContainerName", typeof(Aggregate1), s =>
{
    s.AddEventsSubscription("TestSubscriptionName", "changeFeedName_1", null);
    s.AddEventsSubscription("TestSubscriptionName_2", "changeFeedName_2", DateTime.UtcNow, TimeSpan.FromSeconds(2));
});

c.AddEventsContainerForAggregates("DatabaseName", "EventsContainerName", assembly, s =>
{
    s.AddEventsSubscription("TestSubscriptionName", "changeFeedName_1", null);
});
```

#### Event packing

By default, CosmosDb saves all events created during a single save operation in one packet document:

```csharp
c.EventPacking = EventPackingMode.AllEventsInOnePacket;
```

This reduces the number of written documents, documents read during aggregate rehydration, and items handled by the
change feed processor. If needed, the packing mode can be overridden:

```csharp
c.EventPacking = EventPackingMode.Disabled;
c.EventPacking = EventPackingMode.UniformDistributionFilling;
c.EventPacking = EventPackingMode.AllEventsInOnePacket;
```

#### Event handling

To subscribe to event streams, subscriptions must be added.
All events from the specified events container and provided by specified change feed will be directed to all matching
implementations of `IEventHandler<TEvent>` or to the implementations of `IAnyEventHandler`.
This feature is based on the change feed mechanism of Cosmos DB, please see
the [Cosmos DB documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/introduction).
If the spefified database and container, as well as change feed do not exist, they will be created. The fifth parameter
specifies
from which point in time the change feed will be initialized (if it doesn't exist).
An optional poll interval can also be configured per subscription to define how long the processor waits before checking
for more changes when no changes were found.
For more details, please see [Event Handling](#event-handling) section.

```csharp
c.AddEventsSubscription("TestSubscriptionName", "DatabaseName", "EventsContainerName_1", "changeFeedName_1", null);
c.AddEventsSubscription("TestSubscriptionName", "DatabaseName", "EventsContainerName_2", "changeFeedName_2", DateTime.UtcNow);
c.AddEventsSubscription("TestSubscriptionName", "DatabaseName", "EventsContainerName_3", "changeFeedName_3", null, TimeSpan.FromSeconds(2));
```

#### ExpectedVersion.Any

Due to the specific functioning of the CosmosDb database, it is not possible to implement `ExpectedVersion.Any`
directly.
To emulate the desired behavior, `ExpectedVersion.Any` works like `ExpectedVersion.Retrieved` with retries (in case of
encountering an unexpected version).
As a consequence of this solution, it is necessary to define the number of attempts.
This is done using the configuration parameter `RetryCountForUnexpectedVersionWhenExpectedVersionIsAny`.
By default, this parameter is set to 10.

#### Too many requests

EventForging relies on the default retry behavior of the Azure Cosmos DB SDK for short `429 TooManyRequests`
throttling periods. It does not add its own application-level retry policy around aggregate reads, aggregate writes,
deletes, or change feed handling.

If the SDK exhausts its retry budget and Cosmos DB still returns `429 TooManyRequests`, EventForging throws
`EventForgingCosmosDbTooManyRequestsException`. The exception exposes the `RetryAfter` and `RequestCharge` values when
they are available from the Cosmos DB response.

The application can tune the Cosmos DB SDK retry budget for rate-limited requests:

```csharp
r.UseCosmosDb(c =>
{
    c.ConnectionString = "...";
    c.MaxRetryAttemptsOnRateLimitedRequests = 9;
    c.MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30);
});
```

For command processing and event handlers, the application should still define its own retry, backpressure, or queueing
policy. This is especially important for writes; retries should be paired with a stable `initiatorId` and idempotency
when the caller needs to safely repeat a command after an uncertain result.

## Examples

An example application can be found [here](https://github.com/piotr07da/EventForgingOutcomes-Sample).
