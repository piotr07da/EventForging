// ReSharper disable InconsistentNaming

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests;

public class given_aggregate_in_database : IAsyncLifetime
{
    private readonly Guid _aggregateId;
    private readonly IRepository<BreweryAggregate> _repository;

    public given_aggregate_in_database()
    {
        _repository = ServiceProviderFactory.Create().GetRequiredService<IRepository<BreweryAggregate>>();
        _aggregateId = Guid.NewGuid();
    }

    public async Task InitializeAsync()
    {
        var a = new BreweryAggregate();
        a.BrewNumberBeer(36);
        a.BrewTextBeer("EventForging @#!$@#%!@#%!@#$ 124239");
        a.BrewTimestampBeer(DateTime.UtcNow);

        await _repository.SaveAsync(_aggregateId, a, ExpectedVersion.None, Guid.Empty, Guid.Empty);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task when_Read_and_call_some_operation_and_Save_with_None_expected_version_then_exception_thrown()
    {
        var a = await _repository.GetAsync(_aggregateId);

        a.BrewNumberBeer(-100);

        var ex = await Assert.ThrowsAnyAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(_aggregateId, a, ExpectedVersion.None, Guid.Empty, Guid.Empty);
        });
        Assert.Equal(ExpectedVersion.None, ex.ExpectedVersion);
        Assert.Equal(2, ex.LastReadVersion);
        Assert.Null(ex.ActualVersion);
    }

    [Fact]
    public async Task when_Read_and_call_some_operation_and_Save_with_expected_version_different_than_read_aggregate_version_then_exception_thrown()
    {
        var a = await _repository.GetAsync(_aggregateId);

        a.BrewNumberBeer(-100);

        var ex = await Assert.ThrowsAnyAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(_aggregateId, a, 15, Guid.Empty, Guid.Empty);
        });
        Assert.Equal(15, ex.ExpectedVersion);
        Assert.Equal(2, ex.LastReadVersion);
        Assert.Null(ex.ActualVersion);
    }

    [Fact]
    public async Task when_Read_twice_and_call_some_operation_twice_and_Save_twice_with_Any_expected_version_then_exception_thrown_at_second_Save()
    {
        var a1 = await _repository.GetAsync(_aggregateId);
        var a2 = await _repository.GetAsync(_aggregateId);

        a1.BrewNumberBeer(999);
        a2.BrewNumberBeer(-100);

        await _repository.SaveAsync(_aggregateId, a1, ExpectedVersion.Any, Guid.Empty, Guid.Empty);

        var ex = await Assert.ThrowsAnyAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(_aggregateId, a2, ExpectedVersion.Any, Guid.Empty, Guid.Empty);
        });
        Assert.Equal(ExpectedVersion.Any, ex.ExpectedVersion);
        Assert.Equal(2, ex.LastReadVersion);
        Assert.Equal((AggregateVersion)3, ex.ActualVersion);
    }

    [Fact]
    public async Task when_Read_twice_and_call_some_operation_twice_and_Save_twice_with_NOT_expected_version_then_exception_thrown_at_second_Save()
    {
        var a1 = await _repository.GetAsync(_aggregateId);
        var a2 = await _repository.GetAsync(_aggregateId);

        a1.BrewNumberBeer(999);
        a2.BrewNumberBeer(-100);

        await _repository.SaveAsync(_aggregateId, a1, 2, Guid.Empty, Guid.Empty);

        var ex = await Assert.ThrowsAnyAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await _repository.SaveAsync(_aggregateId, a2, 2, Guid.Empty, Guid.Empty);
        });
        Assert.Equal(2, ex.ExpectedVersion);
        Assert.Equal(2, ex.LastReadVersion);
        Assert.Equal((AggregateVersion)3, ex.ActualVersion);
    }

    [Fact]
    public async Task when_reading_not_existing_aggregate_then_exception_thrown()
    {
        await Assert.ThrowsAnyAsync<AggregateNotFoundEventForgingException>(async () =>
        {
            await _repository.GetAsync(Guid.Empty);
        });
    }
}
