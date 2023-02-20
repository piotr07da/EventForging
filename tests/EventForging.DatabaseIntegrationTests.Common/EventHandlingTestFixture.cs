using System.Runtime.CompilerServices;
using Xunit;

namespace EventForging.DatabaseIntegrationTests.Common
{
    public sealed class EventHandlingTestFixture
    {
        private readonly IRepository<User> _repository;

        public EventHandlingTestFixture(IRepository<User> repository, IEventDatabase eventDatabase)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task when_aggregate_saved_then_events_handled([CallerMemberName] string callerMethod = "")
        {
            Assert.Equal(nameof(when_aggregate_saved_then_events_handled), callerMethod);

            var userId = Guid.NewGuid();
            var userName = Guid.NewGuid().ToString();
            var user = User.RegisterWithName(userId, userName);

            bool IsExpectedUser(UserReadModel u) => u.Id == userId && u.Name == userName;

            var tcs = new TaskCompletionSource();
            UserEventHandlers.RegisterOnEventHandled(userId, (_, _) =>
            {
                if (ReadModel.HasUser(IsExpectedUser))
                {
                    tcs.SetResult();
                }
            });

            await _repository.SaveAsync(userId, user, ExpectedVersion.Any, Guid.Empty, Guid.NewGuid(), cancellationToken: CancellationToken.None);

            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.True(tcs.Task.IsCompleted, "The operation timed out.");
            Assert.True(ReadModel.HasUser(IsExpectedUser), "An aggregate with expected state not found.");
        }
    }
}
