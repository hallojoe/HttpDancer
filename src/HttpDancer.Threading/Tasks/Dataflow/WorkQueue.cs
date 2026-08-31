using System.Threading.Tasks.Dataflow;

namespace HttpDancer.Threading.Tasks.Dataflow;

public sealed class WorkQueue<T> : IAsyncDisposable
{
    private readonly BufferBlock<T> _queue;
    private readonly ActionBlock<T> _workers;

    /// <summary>
    /// Creates a bounded work queue that processes up to <paramref name="maxDegreeOfParallelism"/> items at once.
    /// </summary>
    /// <param name="handlerAsync">Work to run for each item.</param>
    /// <param name="capacity">
    /// Optional max number of queued items. When full, EnqueueAsync waits (backpressure).
    /// </param>
    /// <param name="maxDegreeOfParallelism">How many items may process concurrently.</param>
    /// <param name="cancellationToken">Cancels enqueueing + processing.</param>
    public WorkQueue(
        Func<T, CancellationToken, Task> handlerAsync,
        int? capacity = null,
        int maxDegreeOfParallelism = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handlerAsync);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

        var queueOptions = new DataflowBlockOptions
        {
            CancellationToken = cancellationToken
        };

        if (capacity is { } cap)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap);

            // Limits how many items can be waiting in memory at once.
            // Producers will await when the queue is full.
            queueOptions.BoundedCapacity = cap;
        }

        // This is the "inbox" / queue that accepts items continuously.
        _queue = new BufferBlock<T>(queueOptions);

        // This is the worker pool that actually processes the items.
        _workers = new ActionBlock<T>(
            item => handlerAsync(item, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,

                // Up to X items processed concurrently.
                MaxDegreeOfParallelism = maxDegreeOfParallelism,

                // If you don't care about ordering, this gives better throughput.
                EnsureOrdered = false
            });

        // Connect the queue to the workers.
        // PropagateCompletion means:
        // - when the queue completes, the worker block completes after draining.
        _queue.LinkTo(_workers, new DataflowLinkOptions { PropagateCompletion = true });
    }

    /// <summary>
    /// Enqueues an item. If capacity is reached, this awaits until there is room (backpressure).
    /// </summary>
    public Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
        => _queue.SendAsync(item, cancellationToken);

    /// <summary>
    /// Signals "no more items will be enqueued".
    /// Workers will finish processing everything already queued.
    /// </summary>
    public void Complete() => _queue.Complete();

    /// <summary>
    /// Waits until the queue is drained and all in-flight work is done.
    /// </summary>
    public Task Completion => _workers.Completion;

    /// <summary>
    /// Convenience method: complete the queue and await drain.
    /// </summary>
    public async Task CompleteAndDrainAsync()
    {
        Complete();
        await Completion.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await CompleteAndDrainAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal shutdown path.
        }
    }
}