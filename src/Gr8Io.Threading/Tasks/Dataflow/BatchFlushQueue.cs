using System.Threading.Tasks.Dataflow;

namespace Gr8Io.Threading.Tasks.Dataflow;

/// <summary>
/// Continuously accepts items and flushes them in batches.
///
/// Behavior:
/// - Items are collected in memory until <paramref name="batchSize"/> is reached, then flushed automatically
/// - A manual flush can be triggered at any time to flush whatever is currently buffered
/// - On shutdown, remaining items are flushed before completion
///
/// Typical use case:
/// - File / database logging
/// - Buffered telemetry
/// - Any scenario where writing per-item is too expensive
///
/// This is intentionally similar to how file-based loggers batch writes.
/// </summary>
public sealed class BatchFlushQueue<T> : IAsyncDisposable
{
    // Collects incoming items into batches of a fixed size.
    private readonly BatchBlock<T> _batch;

    // Executes the actual flush logic (ex: write to a file or other operation that cannot run per item).
    // Runs sequentially to avoid concurrent writes.
    private readonly ActionBlock<T[]> _flushBlock;

    /// <summary>
    /// Creates a new batching queue.
    /// </summary>
    /// <param name="batchSize">
    /// Number of items required before an automatic flush occurs.
    /// </param>
    /// <param name="flushAsync">
    /// Callback that performs the actual flush (e.g. write batch to disk).
    /// </param>
    /// <param name="boundedCapacity">
    /// Optional maximum number of items allowed to be buffered.
    /// When exceeded, producers applying backpressure will wait.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the entire pipeline (enqueueing + flushing).
    /// </param>
    public BatchFlushQueue(
        int batchSize,
        Func<IReadOnlyList<T>, CancellationToken, Task> flushAsync,
        int? boundedCapacity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentNullException.ThrowIfNull(flushAsync);

        // Controls how items are grouped into batches.
        var batchOptions = new GroupingDataflowBlockOptions
        {
            CancellationToken = cancellationToken
        };

        if (boundedCapacity is { } cap)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap);

            // Prevent unbounded memory growth if producers outpace flush speed.
            batchOptions.BoundedCapacity = cap;
        }

        // Collects items until either:
        // - batchSize is reached, or
        // - TriggerBatch() is called
        _batch = new BatchBlock<T>(batchSize, batchOptions);

        // Executes the flush logic for each produced batch.
        // MaxDegreeOfParallelism = 1 ensures flushes are serialized
        // (important for file writes, ordered logging, etc.).
        _flushBlock = new ActionBlock<T[]>(
            batchItems => flushAsync(batchItems, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        // When the batch block completes, completion propagates to the flush block.
        _batch.LinkTo(_flushBlock, new DataflowLinkOptions
        {
            PropagateCompletion = true
        });
    }

    /// <summary>
    /// Enqueues an item for batching.
    ///
    /// This method can be called continuously while the queue is alive.
    /// If bounded capacity is configured and the buffer is full,
    /// this call will asynchronously wait (backpressure).
    /// </summary>
    public Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        // SendAsync respects both the caller token and the internal pipeline token.
        return _batch.SendAsync(item, cancellationToken);
    }

    /// <summary>
    /// Forces an immediate flush of whatever items are currently buffered,
    /// even if the batch size has not been reached.
    ///
    /// This is typically used during graceful shutdown.
    /// </summary>
    public void TriggerFlush()
    {
        // Causes BatchBlock to emit the current partial batch immediately.
        _batch.TriggerBatch();
    }

    /// <summary>
    /// Stops accepting new items, flushes remaining buffered items,
    /// and waits for all flush operations to complete.
    ///
    /// This should be called during graceful application shutdown.
    /// </summary>
    public async Task CompleteAndFlushAsync()
    {
        // Emit any partial batch right now.
        _batch.TriggerBatch();

        // Signal that no more items will be enqueued.
        _batch.Complete();

        // Wait until all flushed batches have finished processing.
        await _flushBlock.Completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the queue by flushing remaining items and shutting down the pipeline.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await CompleteAndFlushAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during shutdown is expected and safe to ignore.
        }
    }
}