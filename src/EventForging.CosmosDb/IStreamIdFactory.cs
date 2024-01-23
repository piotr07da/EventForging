namespace EventForging.CosmosDb;

public interface IStreamIdFactory
{
    string Create(Type aggregateType, string aggregateId);
}
