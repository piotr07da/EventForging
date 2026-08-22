namespace EventForging;

/// <summary>Defines where reading an event stream begins.</summary>
public readonly record struct EventStreamReadPosition
{
    private readonly AggregateVersion? _afterVersion;

    private EventStreamReadPosition(AggregateVersion afterVersion)
    {
        _afterVersion = afterVersion;
    }

    /// <summary>Gets the position that reads an event stream from its beginning.</summary>
    public static EventStreamReadPosition Beginning => default;

    /// <summary>Indicates whether the complete event stream will be read.</summary>
    public bool IsBeginning => !_afterVersion.HasValue;

    /// <summary>Creates a position that starts reading after <paramref name="version"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="version"/> does not represent an existing aggregate.</exception>
    public static EventStreamReadPosition After(AggregateVersion version)
    {
        if (version.AggregateDoesNotExist)
        {
            throw new ArgumentException($"{nameof(AggregateVersion.NotExistingAggregate)} cannot be used as an event stream read position.", nameof(version));
        }

        return new EventStreamReadPosition(version);
    }

    /// <summary>Tries to get the version after which reading begins.</summary>
    public bool TryGetAfterVersion(out AggregateVersion version)
    {
        if (_afterVersion.HasValue)
        {
            version = _afterVersion.Value;
            return true;
        }

        version = default;
        return false;
    }
}
