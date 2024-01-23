namespace EventForging.InMemory;

public interface IStreamIdFactory
{
    string Create(Type aggregateType, string aggregateId);
}
