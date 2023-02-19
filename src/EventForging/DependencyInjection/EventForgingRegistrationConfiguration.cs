using System.Reflection;
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
        throw new NotImplementedException();
    }
}
