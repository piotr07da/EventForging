using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace EventForging;

public interface IEventForgingRegistrationConfiguration
{
    IServiceCollection Services { get; }
    void ConfigureEventForging(Action<IEventForgingConfiguration> configure);
    void AddEventHandlers(Assembly assembly);
}
