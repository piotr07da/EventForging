namespace EventForging.Tests;

public class TestAggregate : IEventForged
{
    public TestAggregate()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public int Number { get; private set; }
    public string Text { get; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    public void SingNumber(int number)
    {
        Events.Apply(new NumberChangedEvent(number));
    }

    public void ChangeText(string text)
    {
        Events.Apply(new TextChangedEvent(text));
    }

    public void ChangeTimestamp(DateTime timestamp)
    {
        Events.Apply(new TimestampChangedEvent(timestamp));
    }
}

public class NumberChangedEvent
{
    public NumberChangedEvent(int number)
    {
        Number = number;
    }

    public int Number { get; }
}

public class TextChangedEvent
{
    public TextChangedEvent(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

public class TimestampChangedEvent
{
    public TimestampChangedEvent(DateTime timestamp)
    {
        Timestamp = timestamp;
    }

    public DateTime Timestamp { get; }
}
