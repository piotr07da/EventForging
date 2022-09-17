using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.InMemory.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEventForgingInMemory(this IServiceCollection collection)
        {
            if (collection.Any(d => d.ImplementationType == typeof(InMemoryEventDatabase)))
            {
                throw new Exception($"{nameof(AddEventForgingInMemory)}() has already been called and may only be called once per container.");
            }

            collection.AddTransient<IEventDatabase, InMemoryEventDatabase>();

            return collection;
        }
    }
}
