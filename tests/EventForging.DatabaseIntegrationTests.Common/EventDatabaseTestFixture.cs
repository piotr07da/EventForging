﻿// ReSharper disable InconsistentNaming

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class EventDatabaseTestFixture
{
    private readonly IRepository<User> _repository;
    private readonly IEventDatabase _eventDatabase;

    public EventDatabaseTestFixture(IRepository<User> repository, IEventDatabase eventDatabase)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventDatabase = eventDatabase ?? throw new ArgumentNullException(nameof(eventDatabase));
    }

    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_saved_then_read_aggregate_rehydrated), callerMethod);

        var userId = Guid.NewGuid();

        var newUser = User.Register(userId);
        await _repository.SaveAsync(userId, newUser, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());
        var userAfterSave = await _repository.GetAsync(userId);

        Assert.Equal(userId, userAfterSave.Id);
    }

    public async Task when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(int amountOfCounterEvents, [CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated), callerMethod);

        var userId = Guid.NewGuid();
        var userName = Guid.NewGuid().ToString();

        var newUser = User.RegisterWithName(userId, userName, amountOfCounterEvents);
        await _repository.SaveAsync(userId, newUser, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());
        var userAfterSave = await _repository.GetAsync(userId);

        Assert.Equal(userId, userAfterSave.Id);
        Assert.Equal(userName, userAfterSave.Name);
        Assert.Equal(amountOfCounterEvents - 1, userAfterSave.Counter);
    }

    public async Task when_existing_aggregate_saved_then_read_aggregate_rehydrated([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_existing_aggregate_saved_then_read_aggregate_rehydrated), callerMethod);

        var userId = Guid.NewGuid();

        var existingUser = await prepare_existing_aggregate(userId);
        existingUser.Approve();
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());
        var userAfterSave = await _repository.GetAsync(userId);

        Assert.Equal(userId, userAfterSave.Id);
        Assert.True(userAfterSave.Approved);
    }

    public async Task when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once), callerMethod);

        var userId = Guid.NewGuid();

        var userToSave = User.Register(userId);
        var initiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, initiatorId);
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, initiatorId);

        var events = await GetEventsAsync(userId);
        Assert.Single(events);
        Assert.True(events[0] is UserRegistered ur && ur.UserId == userId);
    }

    public async Task when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once), callerMethod);

        var userId = Guid.NewGuid();

        var existingUser = await prepare_existing_aggregate(userId);
        existingUser.Approve();
        var initiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, initiatorId);
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, initiatorId);

        var events = await GetEventsAsync(userId);
        Assert.Equal(2, events.Length);
        Assert.True(events[0] is UserRegistered ur && ur.UserId == userId);
        Assert.True(events[1] is UserApproved ua && ua.UserId == userId);
    }

    public async Task when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once), callerMethod);

        var userId = Guid.NewGuid();

        var userToSave = User.Register(userId);
        var initiatorId = Guid.NewGuid();
        var saveTasks = new List<Task>();
        for (var i = 0; i < 10; ++i)
        {
            var saveTask = _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, initiatorId);
            saveTasks.Add(saveTask);
        }

        await Task.WhenAll(saveTasks);

        var events = await GetEventsAsync(userId);
        Assert.Single(events);
        Assert.True(events[0] is UserRegistered ur && ur.UserId == userId);
    }

    public async Task when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once), callerMethod);

        var userId = Guid.NewGuid();

        var existingUser = await prepare_existing_aggregate(userId);
        existingUser.Approve();
        var initiatorId = Guid.NewGuid();
        var saveTasks = new List<Task>();
        for (var i = 0; i < 10; ++i)
        {
            var saveId = Guid.NewGuid().ToString();
            var saveTask = _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", saveId }, });
            saveTasks.Add(saveTask);
        }

        await Task.WhenAll(saveTasks);

        var events = await GetEventsAsync(userId);
        Assert.Equal(2, events.Length);
        Assert.True(events[0] is UserRegistered ur && ur.UserId == userId);
        Assert.True(events[1] is UserApproved ua && ua.UserId == userId);
    }

    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving), callerMethod);

        var userId = Guid.NewGuid();

        var userToSave = User.Register(userId);
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Any, Guid.Empty, firstSaveInitiatorId);
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Any, Guid.Empty, secondSaveInitiatorId);
    }

    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving), callerMethod);

        var userId = Guid.NewGuid();

        var userToSave = User.Register(userId);
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, firstSaveInitiatorId);
        await Assert.ThrowsAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, secondSaveInitiatorId);
        });
    }

    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving), callerMethod);

        var userId = Guid.NewGuid();

        var existingUser = await prepare_existing_aggregate(userId);
        existingUser.Approve();
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Any, Guid.Empty, firstSaveInitiatorId);
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Any, Guid.Empty, secondSaveInitiatorId);
    }

    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving([CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving), callerMethod);

        var userId = Guid.NewGuid();

        var existingUser = await prepare_existing_aggregate(userId);
        existingUser.Approve();
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, firstSaveInitiatorId);
        await Assert.ThrowsAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, secondSaveInitiatorId);
        });
    }

    public async Task do_load_test(int numberOfEvents, int numberOfCycles, [CallerMemberName] string callerMethod = "")
    {
        Assert.Equal(nameof(do_load_test), callerMethod);

        var cycleTimes = new List<long>();

        var sw = new Stopwatch();
        sw.Start();

        var userId = Guid.NewGuid();

        User existingUser;

        await prepare_existing_aggregate(userId);

        for (var cycleIndex = 0; cycleIndex < numberOfCycles; ++cycleIndex)
        {
            var cycleSw = new Stopwatch();
            cycleSw.Start();

            existingUser = await _repository.GetAsync(userId);

            for (var eventIndex = 0; eventIndex < numberOfEvents; ++eventIndex)
            {
                existingUser.Approve();
            }

            await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());

            cycleSw.Stop();
            cycleTimes.Add(cycleSw.ElapsedMilliseconds);
        }

        existingUser = await _repository.GetAsync(userId);
        existingUser.Approve();

        await _repository.SaveAsync(userId, existingUser, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());

        sw.Stop();
    }

    private async Task<User> prepare_existing_aggregate(Guid userId)
    {
        var userToSave = User.Register(userId);
        await _repository.SaveAsync(userId, userToSave, ExpectedVersion.Retrieved, Guid.Empty, Guid.NewGuid());
        var existingUser = await _repository.GetAsync(userId);
        return existingUser;
    }

    private async Task<object[]> GetEventsAsync(Guid userId)
    {
        var eventsAsyncEnumerable = _eventDatabase.ReadAsync<User>(userId.ToString());
        var events = new List<object>();
        await foreach (var e in eventsAsyncEnumerable)
        {
            events.Add(e);
        }

        return events.ToArray();
    }
}
