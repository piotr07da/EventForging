namespace EventForging;

public interface IInvocationForwarder<TContext>
{
    Task ForwardAsync(TContext context, CancellationToken cancellationToken);
}
