using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection;

public interface IEventForgingRegistrationConfiguration
{
    IServiceCollection Services { get; }
}
