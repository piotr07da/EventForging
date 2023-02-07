using System;

namespace EventForging.CosmosDb;

internal interface IStreamNameFactory
{
    string Create(Type aggregateType, string aggregateId);
}
