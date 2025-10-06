namespace EventForging.Tests;

public sealed class GenericRepositorySaveInterceptor : IRepositorySaveInterceptor
{
    public async Task SaveAsync<TAggregate>(SaveContext<TAggregate> context, IInvocationForwarder<SaveContext<TAggregate>> next, CancellationToken cancellationToken)
    {
        await next.ForwardAsync(context, cancellationToken);
    }
}
