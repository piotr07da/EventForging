namespace EventForging;

public readonly struct AggregateVersion
{
    private const long NotExistingAggregateValue = -1;

    private AggregateVersion(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public bool AggregateExists => this != NotExistingAggregate;
    public bool AggregateDoesNotExist => this == NotExistingAggregate;

    public override string ToString()
    {
        if (AggregateDoesNotExist) return nameof(NotExistingAggregate);

        return Value.ToString();
    }

    public override bool Equals(object? obj) => obj != null && this == (AggregateVersion)obj;

    public override int GetHashCode() => Value.GetHashCode();

    public static AggregateVersion NotExistingAggregate { get; } = new(NotExistingAggregateValue);

    public static bool operator ==(AggregateVersion lhs, AggregateVersion rhs) => lhs.Value == rhs.Value;
    public static bool operator !=(AggregateVersion lhs, AggregateVersion rhs) => !(lhs == rhs);

    public static implicit operator long(AggregateVersion ev) => ev.Value;

    public static implicit operator AggregateVersion(long v)
    {
        if (v < 0)
        {
            throw new Exception($"Negative version numbers are not allowed. Correct values are: {nameof(NotExistingAggregate)}, and all integers greater or equal to 0.");
        }

        return new AggregateVersion(v);
    }
}
