namespace EventForging.MongoDb;

public sealed class MongoDbEventForgingConfiguration : IMongoDbEventForgingConfiguration
{
    public string? ConnectionString { get; set; }
}
