using System.Runtime.CompilerServices;
using Xunit;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class EventHandlingTestFixture
{
    private readonly IRepository<User> _repository;

    public EventHandlingTestFixture(IRepository<User> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task when_aggregate_saved_then_events_handled(TimeSpan timeout, int amountOfCounterEvents, [CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_aggregate_saved_then_events_handled), callerMethod);

        var userId = Guid.NewGuid();
        var userName = $"NAME_{Guid.NewGuid()}";

        bool IsExpectedUser(SucceedingUserReadModel u)
        {
            return u.Id == userId && u.Name == userName && u.Counter == amountOfCounterEvents - 1 && u.Approved;
        }

        var eventIds = new List<Guid>();

        var tcs = new TaskCompletionSource();
        SucceedingUserEventHandlers.RegisterOnEventHandled(userId, (e, ei) =>
        {
            eventIds.Add(ei.EventId);

            if (e is UserRegistered userRegistered) ReadModel.AddOrUpdateSucceedingReadModel(userRegistered.UserId, u => u.Id = userRegistered.UserId);
            if (e is UserNamed userNamed) ReadModel.AddOrUpdateSucceedingReadModel(userNamed.UserId, u => u.Name = userNamed.Name);
            if (e is UserCounterChanged userCounterChanged) ReadModel.AddOrUpdateSucceedingReadModel(userCounterChanged.UserId, u => u.Counter = userCounterChanged.Counter);
            if (e is UserApproved userApproved) ReadModel.AddOrUpdateSucceedingReadModel(userApproved.UserId, u => u.Approved = true);

            if (ReadModel.HasSucceedingReadModelUser(userId, IsExpectedUser))
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult();
                }
            }
        });

        var user = User.RegisterWithName(userId, userName, amountOfCounterEvents);
        user.Approve();

        await _repository.SaveAsync(userId, user, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid(), cancellationToken: CancellationToken.None);

        await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        Assert.True(tcs.Task.IsCompleted, "The operation timed out.");
        Assert.True(ReadModel.HasSucceedingReadModelUser(userId, IsExpectedUser), "An aggregate with expected state not found.");

        Assert.True(eventIds.All(AnyEventHandler.Handled), "Not all events were handled by AllEventsHandler.");
    }

    public async Task when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success(int firstTwoEventTryCountUntilSuccess, int lastEventsTryCountUntilSuccess, TimeSpan timeout, TimeSpan delayAfterFirstSave, [CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success), callerMethod);

        var userId = Guid.NewGuid();
        var userName = $"NAME_{Guid.NewGuid()}";

        bool IsExpectedUser(FailingUserReadModel u)
        {
            return u.Id == userId && u.Name == userName && u.Approved;
        }

        var tcs = new TaskCompletionSource();
        FailingUserEventHandlers.RegisterOnEventHandled(userId, (e, _) =>
        {
            var tryCount = 0;
            var expectedTryCountReached = false;
            ReadModel.AddOrUpdateFailingReadModel(userId, u =>
            {
                if (e is UserRegistered userRegistered)
                {
                    ++u.UserRegisteredEventHandlingTryCount;
                    tryCount = u.UserRegisteredEventHandlingTryCount;
                    expectedTryCountReached = tryCount >= firstTwoEventTryCountUntilSuccess;
                    if (expectedTryCountReached)
                    {
                        u.Id = userRegistered.UserId;
                    }
                }

                if (e is UserNamed userNamed)
                {
                    ++u.UserNamedEventHandlingTryCount;
                    tryCount = u.UserNamedEventHandlingTryCount;
                    expectedTryCountReached = tryCount >= firstTwoEventTryCountUntilSuccess;
                    if (expectedTryCountReached)
                    {
                        u.Name = userNamed.Name;
                    }
                }

                if (e is UserApproved)
                {
                    ++u.UserApprovedEventHandlingTryCount;
                    tryCount = u.UserApprovedEventHandlingTryCount;
                    expectedTryCountReached = tryCount >= lastEventsTryCountUntilSuccess;
                    if (expectedTryCountReached)
                    {
                        u.Approved = true;
                    }
                }
            });

            if (!expectedTryCountReached)
            {
                throw new Exception($"Handling the '{e.GetType().Name}' event failed at try '{tryCount}'.");
            }

            if (ReadModel.HasFailingReadModelUser(userId, IsExpectedUser))
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult();
                }
            }
        });

        var user = User.RegisterWithName(userId, userName);
        await _repository.SaveAsync(userId, user, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid(), cancellationToken: CancellationToken.None);
        await Task.Delay(delayAfterFirstSave);
        user = await _repository.GetAsync(userId);
        user.Approve();
        await _repository.SaveAsync(userId, user, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid(), cancellationToken: CancellationToken.None);

        await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        Assert.True(tcs.Task.IsCompleted, "The operation timed out.");
        Assert.True(ReadModel.HasFailingReadModelUser(userId, IsExpectedUser), "An aggregate with expected state not found.");
    }
}
