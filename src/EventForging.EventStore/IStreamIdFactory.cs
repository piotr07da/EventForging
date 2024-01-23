namespace EventForging.EventStore;

public interface IStreamIdFactory
{
    string Create(Type aggregateType, string aggregateId);
}
