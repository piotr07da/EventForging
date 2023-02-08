using System;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

internal sealed class EventForgingRegistrationConfiguration : IEventForgingRegistrationConfiguration
{
    public EventForgingRegistrationConfiguration(IServiceCollection services, IEventForgingConfiguration configuration)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IServiceCollection Services { get; }
    public IEventForgingConfiguration Configuration { get; }
}
