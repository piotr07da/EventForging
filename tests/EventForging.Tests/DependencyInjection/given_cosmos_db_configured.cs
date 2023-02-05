// ReSharper disable InconsistentNaming

using EventForging.CosmosDb.DependencyInjection;
using EventForging.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests.DependencyInjection
{
    public class given_cosmos_db_configured
    {
        private readonly IServiceProvider _sp;

        public given_cosmos_db_configured()
        {
            var services = new ServiceCollection();

            services.AddEventForging(efConfiguration =>
            {
                efConfiguration.UseCosmosDb(cosmosConfiguration =>
                {
                    cosmosConfiguration.ConnectionString = "abc123";
                    cosmosConfiguration.AddAggregateLocation("Sales", "Events", typeof(BreweryAggregate));
                });
            });
            _sp = services.BuildServiceProvider();
        }

        [Fact]
        public async Task connection_string_shall_be_configured()
        {
            var breweryRepository = _sp.GetRequiredService<IRepository<BreweryAggregate>>();
            var breweryId = Guid.NewGuid();
            var brewery = new BreweryAggregate();
            brewery.BrewNumberBeer(555);
            await breweryRepository.SaveAsync(breweryId, brewery, ExpectedVersion.None, Guid.NewGuid(), Guid.Empty, null);
        }
    }
}
