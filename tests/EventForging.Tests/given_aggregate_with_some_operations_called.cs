// ReSharper disable InconsistentNaming

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests
{
    public class given_aggregate_with_some_operations_called
    {
        private static readonly int Number = 36;
        private static readonly string Text = "EventForging @#!$@#%!@#%!@#$ 124239";
        private static readonly DateTime Timestamp = DateTime.UtcNow;

        private readonly Guid _aggregateId;
        private readonly IRepository<BreweryAggregate> _repository;
        private readonly BreweryAggregate _aggregate;

        public given_aggregate_with_some_operations_called()
        {
            _aggregateId = Guid.NewGuid();

            var serviceProvider = ServiceProviderFactory.Create();

            _repository = serviceProvider.GetRequiredService<IRepository<BreweryAggregate>>();

            _aggregate = new BreweryAggregate();
            _aggregate.BrewNumberBeer(Number);
            _aggregate.BrewTextBeer(Text);
            _aggregate.BrewTimestampBeer(Timestamp);
        }

        [Fact]
        public async Task when_Save_to_and_Read_from_the_repository_then_aggregate_state_is_restored()
        {
            await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.None, Guid.Empty, Guid.Empty, null);

            var readAggregate = await _repository.GetAsync(_aggregateId);

            Assert.Equal(Number, readAggregate.NumberBeerBrewed);
            Assert.Equal(Text, readAggregate.TextBeerBrewed);
            Assert.Equal(Timestamp, readAggregate.TimestampBeerBrewed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(999)]
        public async Task when_Save_with_unexpected_version_then_throws_an_exception(int unexpectedVersion)
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await _repository.SaveAsync(_aggregateId, _aggregate, unexpectedVersion, Guid.Empty, Guid.Empty, null);
            });
        }

        [Fact]
        public async Task when_Save_once_with_None_expected_version_then_does_not_throw_exception()
        {
            await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.None, Guid.Empty, Guid.Empty, null);
        }

        [Fact]
        public async Task when_Save_twice_with_None_expected_version_then_throws_an_exception()
        {
            await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.None, Guid.Empty, Guid.Empty, null);

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.None, Guid.Empty, Guid.Empty, null);
            });
        }

        [Fact]
        public async Task when_Save_once_with_Any_expected_version_then_does_not_throw_exception()
        {
            await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.Any, Guid.Empty, Guid.Empty, null);
        }

        [Fact]
        public async Task when_Save_twice_with_Any_expected_version_then_throws_an_exception()
        {
            await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.Any, Guid.Empty, Guid.Empty, null);

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await _repository.SaveAsync(_aggregateId, _aggregate, ExpectedVersion.Any, Guid.Empty, Guid.Empty, null);
            });
        }
    }
}
