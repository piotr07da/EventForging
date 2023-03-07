using Microsoft.Extensions.Logging;

namespace EventForging.Logging
{
    internal class NullLogger : ILogger
    {
        private NullLogger()
        {
        }

        public static NullLogger Instance { get; } = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }
    }
}
