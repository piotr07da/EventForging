using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace EventForging.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEventForging(this IServiceCollection collection)
        {
            if (collection.Any(d => d.ServiceType == typeof(IAggregateRehydrator)))
            {
                throw new Exception($"{nameof(AddEventForging)}() has already been called but may only be called once per container.");
            }

            collection.AddTransient(typeof(IAggregateRehydrator), typeof(AggregateRehydrator));
            collection.AddTransient(typeof(IRepository<>), typeof(Repository<>));

            return collection;
        }
    }
}
