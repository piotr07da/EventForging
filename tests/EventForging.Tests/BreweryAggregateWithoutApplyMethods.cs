namespace EventForging.Tests;

public class BreweryAggregateWithoutApplyMethods : IEventForged
{
    public BreweryAggregateWithoutApplyMethods()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

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
}
