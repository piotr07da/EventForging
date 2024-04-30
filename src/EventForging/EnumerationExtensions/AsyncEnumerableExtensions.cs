using System.Runtime.CompilerServices;

namespace EventForging.EnumerationExtensions;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> WithExceptionIntercept<T>(this IAsyncEnumerable<T> source, Action<Exception> intercept, Action finallyCallback, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.ConfigureAwait(false).WithCancellation(cancellationToken).GetAsyncEnumerator();

        while (true)
        {
            T item;

            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;

                item = enumerator.Current;
            }
            catch (Exception ex)
            {
                intercept(ex);
                finallyCallback();
                throw;
            }

            yield return item;
        }

        finallyCallback();
    }
}
