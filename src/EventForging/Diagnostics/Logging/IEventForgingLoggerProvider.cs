using Microsoft.Extensions.Logging;

namespace EventForging.Diagnostics.Logging;

public interface IEventForgingLoggerProvider
{
    ILogger Logger { get; }
}
