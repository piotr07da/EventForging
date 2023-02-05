using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace EventForging.CosmosDb;

public interface IEventForgingCosmosDbConfiguration
{
    string ConnectionString { get; set; }
    IReadOnlyDictionary<Type, AggregateLocationConfiguration> AggregateLocations { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    bool IgnoreServerCertificateValidation { get; set; }

    void AddAggregateLocation(string databaseName, string containerName, params Type[] aggregateTypes);
    void AddAggregatesLocations(string databaseName, string containerName, Assembly aggregatesAssembly, Func<Type, bool> aggregateTypeFilter = default);
}

public sealed record AggregateLocationConfiguration(string DatabaseName, string ContainerName);
