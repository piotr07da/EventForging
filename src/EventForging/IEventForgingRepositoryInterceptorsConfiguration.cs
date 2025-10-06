namespace EventForging;

public interface IEventForgingRepositoryInterceptorsConfiguration
{
    void Register<TInterceptor>()
        where TInterceptor : class, IRepositorySaveInterceptor;

    void Register<TInterceptor, TAggregate>()
        where TInterceptor : class, IRepositorySaveInterceptor<TAggregate>
        where TAggregate : class;
}
