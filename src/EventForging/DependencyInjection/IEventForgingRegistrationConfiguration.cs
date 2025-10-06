using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging;

public interface IEventForgingRegistrationConfiguration
{
    IServiceCollection Services { get; }
    void ConfigureEventForging(Action<IEventForgingConfiguration> configure);
    void AddEventHandlers(Assembly assembly);

    void AddRepositorySaveInterceptor<TInterceptor>()
        where TInterceptor : class, IRepositorySaveInterceptor;

    void AddRepositorySaveInterceptor<TInterceptor, TAggregate>()
        where TInterceptor : class, IRepositorySaveInterceptor<TAggregate>
        where TAggregate : class;
}
