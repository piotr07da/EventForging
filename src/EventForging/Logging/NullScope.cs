using System;

namespace EventForging.Logging;

internal class NullScope : IDisposable
{
    private NullScope()
    {
    }

    public static NullScope Instance { get; } = new();

    public void Dispose()
    {
    }
}
