using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventForging
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NullableLoggerFactoryExtensions
    {
        public static ILogger CreateEventForgingLogger(this ILoggerFactory? loggerFactory) => CreateEventForgingLogger(loggerFactory, "EventForging");

        public static ILogger CreateEventForgingLogger(this ILoggerFactory? loggerFactory, string categoryName)
        {
            if (loggerFactory != null)
            {
                return loggerFactory.CreateLogger(categoryName);
            }

            return NullLogger.Instance;
        }
    }
}
