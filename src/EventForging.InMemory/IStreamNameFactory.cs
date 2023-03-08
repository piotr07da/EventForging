namespace EventForging.InMemory;

public interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
