namespace EventForging.Tests;

public sealed class SpecificRepositorySaveInterceptor : IRepositorySaveInterceptor<BreweryAggregate>
{
    public async Task SaveAsync(SaveContext<BreweryAggregate> context, IInvocationForwarder<SaveContext<BreweryAggregate>> next, CancellationToken cancellationToken)
    {
        await next.ForwardAsync(context, cancellationToken);
    }
}
