using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

public sealed class EventForgingRepositoryInterceptorsConfiguration : IEventForgingRepositoryInterceptorsConfiguration
{
    private readonly IServiceCollection _services;

    internal EventForgingRepositoryInterceptorsConfiguration(IServiceCollection services)
    {
        _services = services;
    }

    public void Register<TInterceptor>()
        where TInterceptor : class, IRepositorySaveInterceptor
    {
        _services.AddSingleton<IRepositorySaveInterceptor, TInterceptor>();
    }

    public void Register<TInterceptor, TAggregate>()
        where TInterceptor : class, IRepositorySaveInterceptor<TAggregate>
        where TAggregate : class
    {
        _services.AddSingleton<IRepositorySaveInterceptor<TAggregate>, TInterceptor>();
    }
}
