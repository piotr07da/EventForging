namespace EventForging.CosmosDb;

public interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
