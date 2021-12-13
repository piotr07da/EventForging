namespace EventForging
{
    public readonly struct ExpectedVersion
    {
        private readonly int _value;

        private ExpectedVersion(int value)
        {
            _value = value;
        }

        public bool IsNone => this == None;
        public bool IsAny => this == Any;

        public override string ToString()
        {
            if (IsNone) return "None";
            if (IsAny) return "Any";
            return _value.ToString();
        }

        public override bool Equals(object obj) => this == (ExpectedVersion)obj;

        public override int GetHashCode() => _value.GetHashCode();

        public static ExpectedVersion None { get; } = new ExpectedVersion(-1);
        public static ExpectedVersion Any { get; } = new ExpectedVersion(-2);

        public static bool operator ==(ExpectedVersion lhs, ExpectedVersion rhs) => lhs._value == rhs._value;
        public static bool operator !=(ExpectedVersion lhs, ExpectedVersion rhs) => !(lhs == rhs);
    }
}
