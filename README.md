# EventForging

EventForging is a free, open-source .NET framework for event sourced applications.

EventForging is MIT licensed.

## Status
[![build-n-publish](https://github.com/piotr07da/EventForging/actions/workflows/build-n-publish.yml/badge.svg)](https://github.com/piotr07da/EventForging/actions/workflows/build-n-publish.yml)

## Features

- InMemory mode
- EventStore integration
- CosmosDb integration
- Opened for another databases
- No inheritance hell - there are no base classes, you don't have to inherit from anything.

## Using EventForging
### Application Layer
To get and save an aggregate from and to the database we use the `IRepository<TAggregate>`.
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
        await _repository.SaveAsync(command.CustomerId, customer, ExpectedVersion.Any, context.ConversationId, context.InitiatorId);
    }
```
### Domain Layer
Every aggregate have to implement the `IEventForged` interface. The simplest form looks like this:
```csharp
public class Customer : IEventForged
{
    private Customer()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }
}
```
Lets add two methods - the first will be a factory method and the second will be a method for renaming a customer. For the simplicity of an example there is no additional logic - just applying the events.

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
        Events.Apply(new CustomerNamed(id.Value, name.Value));
    }
```

The last step is to handle the newly applied events or the events loaded from the database by use of the repository (described above). The purpose of restoring events is to rebuild the current state of an aggregate. You do it by creating a private `Apply` methods.

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

The above example restores the state of an aggregate as a value objects stored in properties but of course you can keep your state in private fields as primitive types or any way you want. The important thing is to keep your code in `Apply` methods simple - that code should never fail becuase it just rebuilds the state from the events that already happend - we cannot negate the past, the past cannot fail. Therefore, in given example the value objects are restored by calling `Restore` methods instead of `FromValue` methods to avoid a domain logic validation.

### Configuration
To enable and configure EventForging we need to use `AddEventForging(...)` extension method.
```csharp

```

