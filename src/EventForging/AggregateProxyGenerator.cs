using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace EventForging;

internal sealed class AggregateProxyGenerator
{
    public const string AggregateProxiesAssemblyName = "EventForging.AggregateProxies";

    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> _types;

    static AggregateProxyGenerator()
    {
        var assemblyName = new AssemblyName(AggregateProxiesAssemblyName);
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule(AggregateProxiesAssemblyName);
        _types = new ConcurrentDictionary<Type, Lazy<Type>>();
    }

    public static TAggregate Create<TAggregate>()
        where TAggregate : class
    {
        var baseType = typeof(TAggregate);

        var constructors = baseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.All(ctor => ctor.GetParameters().Length > 0))
        {
            throw new EventForgingException($"An aggregate of type {baseType.Name} must have public parameterless constructor.");
        }

        var proxyType = _types.GetOrAdd(baseType, _ =>
            new Lazy<Type>(() =>
            {
                var proxyTypeBuilder = _moduleBuilder.DefineType($"{baseType.FullName}Proxy", TypeAttributes.Public, baseType);
                proxyTypeBuilder.DefineField(AggregateMetadata.FieldName, typeof(AggregateMetadata), FieldAttributes.Private);
                return proxyTypeBuilder.CreateTypeInfo()!;
            })
        );
        var aggregate = (TAggregate)Activator.CreateInstance(proxyType.Value);
        aggregate.SetAggregateMetadata(AggregateMetadata.Default());
        return aggregate;
    }
}
