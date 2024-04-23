using Microsoft.Extensions.Logging;

namespace EventForging.Diagnostics.Logging;

public sealed class EventForgingLoggerProvider : IEventForgingLoggerProvider
{
    public EventForgingLoggerProvider(ILoggerFactory? loggerFactory = null)
    {
        if (loggerFactory is null)
        {
            Logger = NullLogger.Instance;
        }
        else
        {
            Logger = loggerFactory.CreateLogger(EventForgingDiagnosticsInfo.LoggerCategoryName);
        }
    }

    public ILogger Logger { get; }
}
