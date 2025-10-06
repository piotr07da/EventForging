namespace EventForging;

internal sealed class RepositorySaveInterceptorContextForwarder<TAggregate> : IRepositorySaveInterceptorContextForwarder<TAggregate>
{
    public bool Forwarded { get; private set; }
    public RepositorySaveInterceptorContext<TAggregate>? ReceivedContext { get; private set; }

    public async Task ForwardAsync(RepositorySaveInterceptorContext<TAggregate> context, CancellationToken cancellationToken)
    {
        Forwarded = true;
        ReceivedContext = context;
        await Task.CompletedTask;
    }
}
