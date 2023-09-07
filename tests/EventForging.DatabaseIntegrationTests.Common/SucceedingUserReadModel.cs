namespace EventForging.DatabaseIntegrationTests.Common;

public sealed record SucceedingUserReadModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public int Counter { get; set; }
    public bool Approved { get; set; }
}
