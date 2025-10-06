namespace EventForging;

public interface IRepositorySaveInterceptor
{
    Task SaveAsync<TAggregate>(RepositorySaveInterceptorContext<TAggregate> context, IRepositorySaveInterceptorContextForwarder<TAggregate> forwarder, CancellationToken cancellationToken);
}

public interface IRepositorySaveInterceptor<TAggregate>
{
    Task SaveAsync(RepositorySaveInterceptorContext<TAggregate> context, IRepositorySaveInterceptorContextForwarder<TAggregate> forwarder, CancellationToken cancellationToken);
}
