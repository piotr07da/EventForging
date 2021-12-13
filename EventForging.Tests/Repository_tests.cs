// ReSharper disable InconsistentNaming

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests
{
    public class Repository_tests
    {
        private readonly Guid _aggregateId;
        private readonly IRepository<TestAggregate> _repository;

        public Repository_tests()
        {
            _aggregateId = Guid.NewGuid();

            var serviceCollection = new ServiceCollection();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            _repository = serviceProvider.GetRequiredService<IRepository<TestAggregate>>();
        }

        [Fact]
        public async Task given_an_aggregate_with_operations_called_when_Save_to_and_Read_from_the_repository_then_aggregate_state_is_restored()
        {
            var number = 36;
            var text = "EventForging @#!$@#%!@#%!@#$ 124239";
            var timestamp = DateTime.UtcNow;

            var a = new TestAggregate();
            a.ChangeNumber(number);
            a.ChangeText(text);
            a.ChangeTimestamp(timestamp);

            await _repository.SaveAsync(_aggregateId, a, ExpectedVersion.None, Guid.Empty, Guid.Empty, null);

            var readAggregate = await _repository.GetAsync(_aggregateId);

            Assert.Equal(number, a.Number);
            Assert.Equal(text, a.Text);
            Assert.Equal(timestamp, a.Timestamp);
        }
    }
}
