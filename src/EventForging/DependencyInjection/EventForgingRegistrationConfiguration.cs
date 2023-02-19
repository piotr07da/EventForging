using System.Reflection;
using EventForging.EventsHandling;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging;

internal sealed class EventForgingRegistrationConfiguration : IEventForgingRegistrationConfiguration
{
    public EventForgingRegistrationConfiguration(IServiceCollection services, IEventForgingConfiguration configuration)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IServiceCollection Services { get; }

    public IEventForgingConfiguration Configuration { get; }

    public void ConfigureEventForging(Action<IEventForgingConfiguration> configure)
    {
        configure(Configuration);
    }

    public void AddEventHandlers(Assembly assembly)
    {
        var eventHandlerType = typeof(IEventHandler);
        var genericEventHandlerType = typeof(IEventHandler<>);
        var ehTypes = assembly.GetTypes().Where(t => t.IsClass && eventHandlerType.IsAssignableFrom(t));
        foreach (var ehType in ehTypes)
        {
            foreach (var ehService in ehType.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericEventHandlerType))
            {
                Services.AddSingleton(ehService, ehType);
            }
        }
    }
}
