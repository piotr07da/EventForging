using System;

namespace EventForging
{
    public readonly struct ExpectedVersion
    {
        private ExpectedVersion(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsNone => this == None;
        public bool IsAny => this == Any;
        public bool IsDefined => !IsNone && !IsAny;

        public override string ToString()
        {
            if (IsNone) return "None";
            if (IsAny) return "Any";
            return Value.ToString();
        }

        public override bool Equals(object obj) => this == (ExpectedVersion)obj;

        public override int GetHashCode() => Value.GetHashCode();

        public static ExpectedVersion None { get; } = new ExpectedVersion(-1);
        public static ExpectedVersion Any { get; } = new ExpectedVersion(-2);

        public static bool operator ==(ExpectedVersion lhs, ExpectedVersion rhs) => lhs.Value == rhs.Value;
        public static bool operator !=(ExpectedVersion lhs, ExpectedVersion rhs) => !(lhs == rhs);

        public static implicit operator ExpectedVersion(int v)
        {
            if (v < 0)
            {
                throw new Exception($"Negative version numbers are not allowed. Correct values are: {nameof(None)}, {nameof(Any)}, and all integers greater or equal to 0.");
            }

            return new ExpectedVersion(v);
        }

        public static implicit operator ulong(ExpectedVersion ev) => (ulong)ev.Value;
        public static implicit operator long(ExpectedVersion ev) => ev.Value;
        public static implicit operator int(ExpectedVersion ev) => (int)ev.Value;
    }
}
