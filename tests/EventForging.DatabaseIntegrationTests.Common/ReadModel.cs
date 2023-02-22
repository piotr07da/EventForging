namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class ReadModel
{
    private static readonly AsyncLocal<IDictionary<Guid, SucceedingUserReadModel>> _succeedingModelUserEntries = new();
    private static readonly AsyncLocal<IDictionary<Guid, FailingUserReadModel>> _failingModelUserEntries = new();

    public static void Initialize()
    {
        _succeedingModelUserEntries.Value = new Dictionary<Guid, SucceedingUserReadModel>();
        _failingModelUserEntries.Value = new Dictionary<Guid, FailingUserReadModel>();
    }

    public static void AddOrUpdateSucceedingReadModel(Guid userId, Action<SucceedingUserReadModel> update)
    {
        var users = _succeedingModelUserEntries.Value;
        if (!users!.TryGetValue(userId, out var user))
        {
            user = new SucceedingUserReadModel();
            users.Add(userId, user);
        }

        update(user);
    }

    public static void AddOrUpdateFailingReadModel(Guid userId, Action<FailingUserReadModel> update)
    {
        var users = _failingModelUserEntries.Value;
        if (!users!.TryGetValue(userId, out var user))
        {
            user = new FailingUserReadModel();
            users.Add(userId, user);
        }

        update(user);
    }

    public static bool HasSucceedingReadModelUser(Guid userId, Func<SucceedingUserReadModel, bool> condition)
    {
        return _succeedingModelUserEntries.Value!.TryGetValue(userId, out var user) && condition(user);
    }

    public static bool HasFailingReadModelUser(Guid userId, Func<FailingUserReadModel, bool> condition)
    {
        return _failingModelUserEntries.Value!.TryGetValue(userId, out var user) && condition(user);
    }
}
