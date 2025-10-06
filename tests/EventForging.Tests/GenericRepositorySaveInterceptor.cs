namespace EventForging.Tests;

public sealed class GenericRepositorySaveInterceptor : IRepositorySaveInterceptor
{
    public async Task SaveAsync<TAggregate>(RepositorySaveInterceptorContext<TAggregate> context, IRepositorySaveInterceptorContextForwarder<TAggregate> forwarder, CancellationToken cancellationToken)
    {
        await forwarder.ForwardAsync(context, cancellationToken);
    }
}
