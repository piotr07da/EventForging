namespace EventForging.EventStore;

internal interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
