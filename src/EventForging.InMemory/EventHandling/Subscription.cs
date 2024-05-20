using System.Collections.Concurrent;
using System.Diagnostics;
using EventForging.Diagnostics.Tracing;
using EventForging.EventsHandling;
using Microsoft.Extensions.Logging;

namespace EventForging.InMemory.EventHandling;

internal sealed class Subscription
{
    private readonly string _name;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger _logger;
    private readonly BlockingCollection<Entry> _queue;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _workerTask;

    public Subscription(
        string name,
        IEventDispatcher eventDispatcher,
        ILogger logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        _logger = logger;
        _queue = new BlockingCollection<Entry>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _workerTask = Task.Run(async () =>
        {
            foreach (var entry in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                var succeeded = false;
                while (!succeeded)
                {
                    // ReSharper disable once ExplicitCallerInfoArgument
                    var activity = EventForgingActivitySourceProvider.ActivitySource.StartActivity(ActivityKind.Consumer, entry.EventInfo.CustomProperties.RestoreActivityContext(), name: "EF Mem Receive Event");
                    try
                    {
                        await _eventDispatcher.DispatchAsync(_name, entry.EventData, entry.EventInfo, _cancellationTokenSource.Token);
                        succeeded = true;
                    }
                    catch (Exception ex)
                    {
                        activity?.RecordException(ex);
                        await Task.Delay(TimeSpan.FromMilliseconds(500), _cancellationTokenSource.Token);
                        _logger.LogError(ex, ex.Message);
                    }
                    finally
                    {
                        activity?.Complete();
                    }
                }
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource!.Cancel();

        return Task.WhenAny(_workerTask!, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public void Send(object eventData, EventInfo eventInfo)
    {
        _queue.Add(new Entry(eventData, eventInfo));
    }

    private sealed record Entry(object EventData, EventInfo EventInfo);
}
