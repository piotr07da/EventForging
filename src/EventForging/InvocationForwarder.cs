namespace EventForging;

internal sealed class InvocationForwarder<TContext> : IInvocationForwarder<TContext>
{
    private readonly Func<TContext, CancellationToken, Task> _next;

    public InvocationForwarder(Func<TContext, CancellationToken, Task> next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task ForwardAsync(TContext context, CancellationToken cancellationToken)
    {
        await _next(context, cancellationToken).ConfigureAwait(false);
    }
}
