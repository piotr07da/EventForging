namespace EventForging;

internal sealed class AggregateMetadata
{
    public const string FieldName = "__EventForging__Metadata";

    private AggregateMetadata()
    {
    }

    public AggregateVersion ReadVersion { get; set; }

    public static AggregateMetadata Default()
    {
        return new AggregateMetadata
        {
            ReadVersion = AggregateVersion.NotExistingAggregate,
        };
    }
}
