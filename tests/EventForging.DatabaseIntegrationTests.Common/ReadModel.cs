using System.Collections.Concurrent;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class ReadModel
{
    private static readonly ConcurrentDictionary<Guid, SucceedingUserReadModel> _succeedingModelUserEntries = new();
    private static readonly ConcurrentDictionary<Guid, FailingUserReadModel> _failingModelUserEntries = new();

    public static void AddOrUpdateSucceedingReadModel(Guid userId, Action<SucceedingUserReadModel> update)
    {
        var users = _succeedingModelUserEntries;
        if (!users!.TryGetValue(userId, out var user))
        {
            user = new SucceedingUserReadModel();
            users.TryAdd(userId, user);
        }

        update(user);
    }

    public static void AddOrUpdateFailingReadModel(Guid userId, Action<FailingUserReadModel> update)
    {
        var users = _failingModelUserEntries;
        if (!users!.TryGetValue(userId, out var user))
        {
            user = new FailingUserReadModel();
            users.TryAdd(userId, user);
        }

        update(user);
    }

    public static bool HasSucceedingReadModelUser(Guid userId, Func<SucceedingUserReadModel, bool> condition)
    {
        return _succeedingModelUserEntries.TryGetValue(userId, out var user) && condition(user);
    }

    public static bool HasFailingReadModelUser(Guid userId, Func<FailingUserReadModel, bool> condition)
    {
        return _failingModelUserEntries.TryGetValue(userId, out var user) && condition(user);
    }
}
