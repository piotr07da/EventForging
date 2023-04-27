namespace EventForging;

public interface IRepository<TAggregate>
{
    /// <summary>Gets the aggregate from the repository.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An instance of the aggregate.</returns>
    Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Gets the aggregate from the repository.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An instance of the aggregate.</returns>
    Task<TAggregate> GetAsync(string aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Tries to get the aggregate from the repository.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An instance of the aggregate or NULL if the aggregate does not exist.</returns>
    Task<TAggregate?> TryGetAsync(Guid aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Tries to get the aggregate from the repository.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An instance of the aggregate or NULL if the aggregate does not exist.</returns>
    Task<TAggregate?> TryGetAsync(string aggregateId, CancellationToken cancellationToken = default);

    /// <summary>Saves an aggregate to the repository with an expected version.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="aggregate">The aggregate that will be saved to the repository.</param>
    /// <param name="expectedVersion">
    ///     The expected version of the aggregate in the repository. Possible values are:
    ///     <list type="bullet">
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.None">ExpectedVersion.None</see>
    ///             </term>
    ///             <description>If you expect that the aggregate does not exist in the repository. This is the case for newly created aggregates.</description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.Any">ExpectedVersion.Any</see>
    ///             </term>
    ///             <description>If you do not want to check the version of the aggregate during save.</description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.Retrieved">ExpectedVersion.Retrieved</see>
    ///             </term>
    ///             <description>If you want the version of the saved aggregate to match the version it had when it was retrieved from the repository. This is similar to Any, but ensures that the version of the aggregate does not change between retrieving and saving.</description>
    ///         </item>
    ///         <item>
    ///             <term>A specific version number</term> <description>If you expect a specific version.</description>
    ///         </item>
    ///     </list>
    /// </param>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <param name="initiatorId">The ID of the initiator.</param>
    /// <param name="customProperties"></param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A Task representing the asynchronous save operation.</returns>
    Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);

    /// <summary>Saves an aggregate to the repository with an expected version.</summary>
    /// <param name="aggregateId">The identifier of the aggregate.</param>
    /// <param name="aggregate">The aggregate that will be saved to the repository.</param>
    /// <param name="expectedVersion">
    ///     The expected version of the aggregate in the repository. Possible values are:
    ///     <list type="bullet">
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.None">ExpectedVersion.None</see>
    ///             </term>
    ///             <description>If you expect that the aggregate does not exist in the repository. This is the case for newly created aggregates.</description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.Any">ExpectedVersion.Any</see>
    ///             </term>
    ///             <description>If you do not want to check the version of the aggregate during save.</description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="ExpectedVersion.Retrieved">ExpectedVersion.Retrieved</see>
    ///             </term>
    ///             <description>If you want the version of the saved aggregate to match the version it had when it was retrieved from the repository. This is similar to Any, but ensures that the version of the aggregate does not change between retrieving and saving.</description>
    ///         </item>
    ///         <item>
    ///             <term>A specific version number</term> <description>If you expect a specific version.</description>
    ///         </item>
    ///     </list>
    /// </param>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <param name="initiatorId">The ID of the initiator.</param>
    /// <param name="customProperties"></param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A Task representing the asynchronous save operation.</returns>
    Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null, CancellationToken cancellationToken = default);
}
