namespace EventForging.Tests;

public class TestAggregate
{
    public int Number { get; private set; }
    public string Text { get; private set; }
    public DateTime Timestamp { get; private set; }

    public void ChangeNumber(int number)
    {
        Number = number;
    }

    public void ChangeText(string text)
    {
        Text = text;
    }

    public void ChangeTimestamp(DateTime timestamp)
    {
        Timestamp = timestamp;
    }
}
