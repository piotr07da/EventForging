namespace EventForging;

public interface IDestructiveEventDatabase
{
    Task DeleteAsync<TAggregate>(string aggregateId, EventsDeletionMode deletionMode, CancellationToken cancellationToken = default);
}
