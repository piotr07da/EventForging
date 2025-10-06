namespace EventForging;

public interface IRepositorySaveInterceptorContextForwarder<TAggregate>
{
    Task ForwardAsync(RepositorySaveInterceptorContext<TAggregate> context);
}
