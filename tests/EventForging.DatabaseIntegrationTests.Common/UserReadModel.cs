namespace EventForging.DatabaseIntegrationTests.Common;

public sealed record UserReadModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}
