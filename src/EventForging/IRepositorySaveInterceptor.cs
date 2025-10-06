namespace EventForging;

public interface IRepositorySaveInterceptor
{
    Task SaveAsync<TAggregate>(SaveContext<TAggregate> context, IInvocationForwarder<SaveContext<TAggregate>> next, CancellationToken cancellationToken);
}

public interface IRepositorySaveInterceptor<TAggregate>
{
    Task SaveAsync(SaveContext<TAggregate> context, IInvocationForwarder<SaveContext<TAggregate>> next, CancellationToken cancellationToken);
}
