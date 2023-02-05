using System;

namespace EventForging;

public class EventForgingConfigurationException : EventForgingException
{
    public EventForgingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EventForgingConfigurationException(string message)
        : base(message)
    {
    }
}
