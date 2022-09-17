namespace EventForging
{
    public class AggregateMetadata
    {
        public const string FieldName = "__Neuca_ES_Metadata";

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
}
