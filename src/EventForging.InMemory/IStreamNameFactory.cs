namespace EventForging.InMemory;

internal interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
