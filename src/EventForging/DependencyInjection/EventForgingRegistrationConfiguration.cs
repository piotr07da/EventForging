using System;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

internal sealed class EventForgingRegistrationConfiguration : IEventForgingRegistrationConfiguration
{
    private readonly IEventForgingConfiguration _configuration;

    public EventForgingRegistrationConfiguration(IServiceCollection services, IEventForgingConfiguration configuration)
    {
        _configuration = configuration;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IServiceCollection Services { get; }
}
