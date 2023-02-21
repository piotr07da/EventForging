namespace EventForging.EventStore;

public interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
