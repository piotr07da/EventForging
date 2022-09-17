using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace EventForging;

public class AggregateProxyGenerator
{
    public const string AggregateProxiesAssemblyName = "EventForging.AggregateProxies";

    private static readonly AssemblyBuilder _assemblyBuilder;
    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> _types;

    static AggregateProxyGenerator()
    {
        var assemblyName = new AssemblyName(AggregateProxiesAssemblyName);
        _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = _assemblyBuilder.DefineDynamicModule(AggregateProxiesAssemblyName);
        _types = new ConcurrentDictionary<Type, Lazy<Type>>();
    }

    public static TAggregate Create<TAggregate>()
        where TAggregate : class, new()
    {
        var baseType = typeof(TAggregate);

        var proxyType = _types.GetOrAdd(baseType, _ =>
            new Lazy<Type>(() =>
            {
                var proxyTypeBuilder = _moduleBuilder.DefineType($"{baseType.FullName}Proxy", TypeAttributes.Public, baseType);
                proxyTypeBuilder.DefineField(AggregateMetadata.FieldName, typeof(AggregateMetadata), FieldAttributes.Private);
                return proxyTypeBuilder.CreateTypeInfo();
            })
        );
        var aggregate = (TAggregate)Activator.CreateInstance(proxyType.Value);
        aggregate.SetAggregateMetadata(AggregateMetadata.Default());
        return aggregate;
    }
}
