namespace EventForging.Tests;

public sealed class SpecificRepositorySaveInterceptor : IRepositorySaveInterceptor<BreweryAggregate>
{
    public async Task SaveAsync(RepositorySaveInterceptorContext<BreweryAggregate> context, IRepositorySaveInterceptorContextForwarder<BreweryAggregate> forwarder, CancellationToken cancellationToken)
    {
        await forwarder.ForwardAsync(context, cancellationToken);
    }
}
