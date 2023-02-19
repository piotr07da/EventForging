namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class ReadModel
{
    private static readonly AsyncLocal<IDictionary<Guid, UserReadModel>> _users = new();

    public static void Initialize()
    {
        _users.Value = new Dictionary<Guid, UserReadModel>();
    }

    public static void AddOrUpdate(Guid userId, Action<UserReadModel> update)
    {
        var users = _users.Value;
        if (!users!.TryGetValue(userId, out var user))
        {
            user = new UserReadModel();
            users.Add(userId, user);
        }

        update(user);
    }

    public static bool HasUser(Func<UserReadModel, bool> condition)
    {
        foreach (var user in _users.Value!.Values)
        {
            if (condition(user))
            {
                return true;
            }
        }

        return false;
    }
}
