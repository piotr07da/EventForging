using System;

namespace EventForging;

public class EventForgingConcurrencyException : EventForgingException
{
    public EventForgingConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EventForgingConcurrencyException(string message)
        : base(message)
    {
    }
}
