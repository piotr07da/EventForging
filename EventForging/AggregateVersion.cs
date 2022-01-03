using System;

namespace EventForging
{
    public readonly struct AggregateVersion
    {
        private const int NotExistingAggregateValue = -1;

        private AggregateVersion(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool AggregateExists => this != NotExistingAggregate;
        public bool AggregateDoesNotExist => this == NotExistingAggregate;

        public override string ToString()
        {
            if (AggregateDoesNotExist) return nameof(NotExistingAggregate);

            return Value.ToString();
        }

        public override bool Equals(object obj) => this == (AggregateVersion)obj;

        public override int GetHashCode() => Value.GetHashCode();

        public static AggregateVersion NotExistingAggregate { get; } = new AggregateVersion(NotExistingAggregateValue);

        public static bool operator ==(AggregateVersion lhs, AggregateVersion rhs) => lhs.Value == rhs.Value;
        public static bool operator !=(AggregateVersion lhs, AggregateVersion rhs) => !(lhs == rhs);


        public static implicit operator ulong(AggregateVersion ev) => (ulong)ev.Value;
        public static implicit operator AggregateVersion(ulong ev) => (int)ev;
        public static implicit operator long(AggregateVersion ev) => ev.Value;
        public static implicit operator AggregateVersion(long ev) => (int)ev;
        public static implicit operator int(AggregateVersion ev) => ev.Value;

        public static implicit operator AggregateVersion(int v)
        {
            if (v < 0)
            {
                throw new Exception($"Negative version numbers are not allowed. Correct values are: {nameof(NotExistingAggregate)}, and all integers greater or equal to 0.");
            }

            return new AggregateVersion(v);
        }
    }
}
