namespace EventForging.InMemory;

internal sealed class DefaultStreamNameFactory : IStreamNameFactory
{
    public string Create(Type aggregateType, string aggregateId)
    {
        return $"{aggregateType.Name}-{aggregateId}";
    }
}
