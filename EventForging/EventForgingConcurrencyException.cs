﻿using System;

namespace EventForging
{
    public class EventForgingConcurrencyException : Exception
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
}
