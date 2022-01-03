namespace EventForging.Tests;

public class BreweryAggregate : IEventForged
{
    public BreweryAggregate()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public int NumberBeerBrewed { get; private set; }
    public string TextBeerBrewed { get; private set; } = string.Empty;
    public DateTime TimestampBeerBrewed { get; private set; }

    public void BrewNumberBeer(int number)
    {
        Events.Apply(new NumberBeerBrewedEvent(number));
    }

    public void BrewTextBeer(string text)
    {
        Events.Apply(new TextBeerBrewedEvent(text));
    }

    public void BrewTimestampBeer(DateTime timestamp)
    {
        Events.Apply(new TimestampBeerBrewedEvent(timestamp));
    }

    private void Apply(NumberBeerBrewedEvent e)
    {
        NumberBeerBrewed = e.Number;
    }

    private void Apply(TextBeerBrewedEvent e)
    {
        TextBeerBrewed = e.Text;
    }

    private void Apply(TimestampBeerBrewedEvent e)
    {
        TimestampBeerBrewed = e.Timestamp;
    }
}

public class NumberBeerBrewedEvent
{
    public NumberBeerBrewedEvent(int number)
    {
        Number = number;
    }

    public int Number { get; }
}

public class TextBeerBrewedEvent
{
    public TextBeerBrewedEvent(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

public class TimestampBeerBrewedEvent
{
    public TimestampBeerBrewedEvent(DateTime timestamp)
    {
        Timestamp = timestamp;
    }

    public DateTime Timestamp { get; }
}
