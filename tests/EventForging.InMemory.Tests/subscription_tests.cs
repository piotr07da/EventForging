// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.InMemory.Tests;

public sealed class subscription_tests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRepository<User> _userRepository;

    public subscription_tests()
    {
        var services = new ServiceCollection();
        var assembly = typeof(User).Assembly;
        services.AddEventForging(r =>
        {
            r.ConfigureEventForging(c =>
            {
                c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(assembly));
            });
            r.UseInMemory(c =>
            {
                c.SerializationEnabled = true;
                c.AddEventSubscription("TestSubscription");
            });
            r.AddEventHandlers(assembly);
        });
        services.AddSingleton<EventDatabaseTestFixture>();
        _serviceProvider = services.BuildServiceProvider();
        _userRepository = _serviceProvider.GetRequiredService<IRepository<User>>();
        ReadModel.Initialize();
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled()
    {
        var userId = Guid.NewGuid();
        var userName = Guid.NewGuid().ToString();
        var user = User.RegisterWithName(userId, userName);
        await _userRepository.SaveAsync(userId, user, ExpectedVersion.Any, Guid.Empty, Guid.NewGuid());

        Assert.True(ReadModel.HasUser(u => u.Id == userId && u.Name == userName));
    }
}
