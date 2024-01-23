namespace EventForging.InMemory;

internal sealed class DefaultStreamIdFactory : IStreamIdFactory
{
    public string Create(Type aggregateType, string aggregateId)
    {
        return $"{aggregateType.Name}-{aggregateId}";
    }
}
