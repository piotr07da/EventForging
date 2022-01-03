using System;
using System.Reflection;

namespace EventForging
{
    internal static class AggregateMetadataExtensions
    {
        public static void ConfigureAggregateMetadata(this object aggregate, Action<AggregateMetadata> configurator)
        {
            var md = GetAggregateMetadata(aggregate);
            configurator(md);
        }

        public static AggregateMetadata GetAggregateMetadata(this object aggregate)
        {
            var f = TryGetMetadataField(aggregate);
            if (f == null)
            {
                return AggregateMetadata.Default();
            }

            return (AggregateMetadata)f.GetValue(aggregate);
        }

        public static void SetAggregateMetadata(this object aggregate, AggregateMetadata metadata)
        {
            var f = GetMetadataField(aggregate);
            f.SetValue(aggregate, metadata);
        }

        private static FieldInfo GetMetadataField(object aggregate)
        {
            var f = TryGetMetadataField(aggregate);
            return f ?? throw new InvalidOperationException($"Aggregate metadata field {AggregateMetadata.FieldName} doesn't exist.");
        }

        private static FieldInfo TryGetMetadataField(object aggregate)
        {
            var t = aggregate.GetType();
            var f = t.GetField(AggregateMetadata.FieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return f;
        }
    }
}
