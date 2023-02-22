namespace EventForging.DatabaseIntegrationTests.Common;

public sealed record FailingUserReadModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool Approved { get; set; }
    public int UserRegisteredEventHandlingTryCount { get; set; }
    public int UserNamedEventHandlingTryCount { get; set; }
    public int UserApprovedEventHandlingTryCount { get; set; }
}
