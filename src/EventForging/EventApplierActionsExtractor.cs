using System.Reflection;

namespace EventForging;

internal static class EventApplierActionsExtractor
{
    public static IReadOnlyDictionary<Type, EventApplierAction> Extract(object extractionSource)
    {
        var extractionSourceType = extractionSource.GetType();
        if (string.IsNullOrEmpty(extractionSourceType.Assembly.FullName))
        {
            throw new EventForgingException("Empty type name of event-applier-actions extraction-source.");
        }

        if (extractionSourceType.Assembly.FullName.StartsWith(AggregateProxyGenerator.AggregateProxiesAssemblyName))
        {
            extractionSourceType = extractionSourceType.BaseType;
        }

        var applyMethods = extractionSourceType!
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == "Apply");

        var extractedApplyDelegates = new Dictionary<Type, EventApplierAction>();

        foreach (var m in applyMethods)
        {
            if (m.ReturnType != typeof(void))
                throw new EventForgingException("All aggregate Apply methods must have void return type.");

            var parameters = m.GetParameters();

            if (parameters.Length != 1)
                throw new EventForgingException("All aggregate Apply methods must have exactly one argument.");

            var prm = parameters[0];

            extractedApplyDelegates.Add(prm.ParameterType, evt => m.Invoke(extractionSource, new[] { evt, }));
        }

        return extractedApplyDelegates;
    }
}
