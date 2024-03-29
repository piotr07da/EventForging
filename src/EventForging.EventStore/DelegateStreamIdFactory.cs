﻿namespace EventForging.EventStore;

internal sealed class DelegateStreamIdFactory : IStreamIdFactory
{
    private readonly Func<Type, string, string> _delegateFactory;

    public DelegateStreamIdFactory(Func<Type, string, string> delegateFactory)
    {
        _delegateFactory = delegateFactory ?? throw new ArgumentNullException(nameof(delegateFactory));
    }

    public string Create(Type aggregateType, string aggregateId)
    {
        return _delegateFactory(aggregateType, aggregateId);
    }
}
