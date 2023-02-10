namespace EventForging.EventStore;

internal sealed class DefaultStreamNameFactory : IStreamNameFactory
{
    public static DefaultStreamNameFactory Instance { get; } = new();

    public string Create(Type aggregateType, string aggregateId)
    {
        return $"{aggregateType.Name}-{aggregateId}";
    }
}
