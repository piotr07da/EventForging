using System;

namespace EventForging;

public class EventForgingException : Exception
{
    public EventForgingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EventForgingException(string message)
        : base(message)
    {
    }
}
