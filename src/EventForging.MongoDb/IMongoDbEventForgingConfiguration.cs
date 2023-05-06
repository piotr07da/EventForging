namespace EventForging.MongoDb;

public interface IMongoDbEventForgingConfiguration
{
    string? ConnectionString { get; set; }
}
