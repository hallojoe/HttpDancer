using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// An enhanced runner for large schedules that:
/// - Uses <see cref="Stopwatch"/> for monotonic timing (immune to system clock changes)
/// - Batches offsets that fall within a tolerance window so "equal-ish" entries run together
/// - Validates inputs (rejects negative offsets, invalid capacities) and avoids precision loss
///
/// Time units:
/// - Schedule offsets are expressed as <see cref="TimeSpan"/> (i.e. 100 ns ticks).
/// - The optional start timestamp must be a stopwatch timestamp from <see cref="Stopwatch.GetTimestamp"/>.
/// </summary>
public sealed class RatedScheduleRunner(RatedScheduleRunnerSettings? ratedScheduleRunnerSettings = null)
    : IRatedScheduleRunner
{
    // Sorting rule when we materialize schedules:
    // 1) run by "bucketed" time (batched window), then
    // 2) by original index to keep deterministic ordering within a bucket.
    private static readonly IComparer<ScheduledItem> ScheduledItemComparer = new ScheduledItemBucketThenIndexComparer();

    // Default runner behavior when no per-run settings are provided.
    private readonly RatedScheduleRunnerSettings _defaults = ratedScheduleRunnerSettings ?? new();

    /// <inheritdoc />
    public Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        RatedScheduleRunnerSettings settings,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        // Treat the provided settings as per-run overrides for ALL runner behavior (including batching).
        return RunInternalAsync(
            schedule,
            executeAsync,
            startStopwatchTimestamp: startTimestamp,
            effectiveSettings: settings,
            assumeSortedSchedule: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        // Use defaults for ALL runner behavior.
        return RunInternalAsync(
            schedule,
            executeAsync,
            startStopwatchTimestamp: startTimestamp,
            effectiveSettings: _defaults,
            assumeSortedSchedule: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        int maxDegreeOfParallelism = 100,
        int? boundedCapacity = null,
        CancellationToken cancellationToken = default)
    {
        // Keep this overload for compatibility, but apply overrides consistently.
        var effective = Merge(_defaults, maxDegreeOfParallelism, boundedCapacity);

        return RunInternalAsync(
            schedule,
            executeAsync,
            startStopwatchTimestamp: startTimestamp,
            effectiveSettings: effective,
            assumeSortedSchedule: false,
            cancellationToken);
    }

    /// <summary>
    /// Optimized path for schedules that are already sorted (non-decreasing offsets).
    /// This avoids materializing and sorting the full schedule, reducing memory footprint.
    /// </summary>
    public Task<long> RunSortedAsync(
        IEnumerable<TimeSpan> sortedSchedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        RatedScheduleRunnerSettings? settingsOverride = null,
        long? startStopwatchTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        // If caller didn't override, use runner defaults.
        var effective = settingsOverride ?? _defaults;

        return RunInternalAsync(
            sortedSchedule,
            executeAsync,
            startStopwatchTimestamp,
            effective,
            assumeSortedSchedule: true,
            cancellationToken);
    }

    private async Task<long> RunInternalAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startStopwatchTimestamp,
        RatedScheduleRunnerSettings effectiveSettings,
        bool assumeSortedSchedule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(executeAsync);

        // Fail fast if the provided settings don't make sense (e.g. non-positive parallelism).
        ValidateEffectiveSettings(effectiveSettings);

        // We measure "time since start" using Stopwatch ticks (monotonic).
        // If the caller doesn't provide a start point, we start "now".
        var effectiveStartStopwatchTimestamp = startStopwatchTimestamp ?? Stopwatch.GetTimestamp();

        // Catch obvious mistakes like passing DateTime.UtcNow.Ticks.
        ValidateStartStopwatchTimestamp(effectiveStartStopwatchTimestamp);

        // Configure the Dataflow ActionBlock that actually executes work items.
        // Think of it as a bounded / throttled async worker queue.
        var executionOptions = new ExecutionDataflowBlockOptions
        {
            // This token cancels:
            // - DelayUntilAsync waits (because we pass it through)
            // - posting to the block (SendAsync observes it)
            // - work inside the block if executeAsync respects the token
            CancellationToken = cancellationToken,

            // How many executeAsync calls are allowed to run at the same time.
            MaxDegreeOfParallelism = effectiveSettings.MaxDegreeOfParallelism,

            // We don't need completion order to match input order.
            // Turning ordering off allows higher throughput.
            EnsureOrdered = false
        };

        if (effectiveSettings.BoundedCapacity is { } cap)
        {
            // Backpressure: if the queue fills up, SendAsync will then await until there is room.
            // This prevents unbounded memory usage when the schedule is huge / execution is slow.
            executionOptions.BoundedCapacity = cap;
        }

        var executionBlock = new ActionBlock<ScheduledItem>(
            async item =>
            {
                // Convert stored ticks back to a TimeSpan for the callback.
                var offset = TimeSpan.FromTicks(item.OffsetTimeSpanTicks);

                // Execute the user callback for this schedule entry.
                // Important: we pass the same cancellationToken, so shutdown/stop can interrupt in-flight work.
                await executeAsync(item.Index, offset, cancellationToken).ConfigureAwait(false);
            },
            executionOptions);

        try
        {
            // Producer side:
            // - waits until each time bucket becomes "due"
            // - posts items into the ActionBlock (subject to backpressure)
            if (assumeSortedSchedule)
            {
                // Streaming mode: schedule is already sorted => we can walk it once with minimal allocations.
                await RunSortedStreamingAsync(
                        schedule,
                        executionBlock,
                        effectiveStartStopwatchTimestamp,
                        effectiveSettings,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Unsorted mode: we materialize, bucket, and sort so we can wait per bucket in order.
                await RunUnsortedMaterializeAndSortAsync(
                        schedule,
                        executionBlock,
                        effectiveStartStopwatchTimestamp,
                        effectiveSettings,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is not an error here.
            // This is the normal path when the app is shutting down or the user manually stops execution.
        }
        finally
        {
            // Tell the ActionBlock "no more items will be posted".
            // This is required so Completion can transition to a final state (RanToCompletion/Faulted/Canceled).
            executionBlock.Complete();
        }

        try
        {
            // Consumer side:
            // Wait for all already-posted items to finish executing.
            // (If you want a "hard stop" mode, you would skip this await when canceled.)
            await executionBlock.Completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Depending on timing, the block may surface cancellation here too.
            // We still treat that as normal shutdown.
        }

        // Return the stopwatch start point so callers can relate offsets to the same origin if needed.
        return effectiveStartStopwatchTimestamp;
    }

    private static async Task RunSortedStreamingAsync(
        IEnumerable<TimeSpan> sortedSchedule,
        ITargetBlock<ScheduledItem> executionBlock,
        long startStopwatchTimestamp,
        RatedScheduleRunnerSettings settings,
        CancellationToken cancellationToken)
    {
        // Bucket size in ticks (0 means "no bucketing").
        var toleranceTimeSpanTicks = settings.BatchTolerance.Ticks;

        // Index is the original position in the input sequence (used by executeAsync).
        var index = 0;

        // Tracks which bucket we've already waited for,
        // so we only delay once per bucket and then post all items in that bucket.
        long? currentBatchBucketTicks = null;

        foreach (var offset in sortedSchedule)
        {
            // Stop fast on shutdown/manual cancel.
            cancellationToken.ThrowIfCancellationRequested();

            var offsetTimeSpanTicks = offset.Ticks;

            // Negative offsets don't make sense for "time since start".
            // Either reject them, or skip them if settings says so.
            if (offsetTimeSpanTicks < 0)
            {
                if (!settings.SkipNegativeOffsets)
                {
                    throw new ArgumentOutOfRangeException(nameof(sortedSchedule),
                        $"Offset at index {index} is negative: {offset}");
                }

                index = checked(index + 1);
                continue;
            }

            // Because the schedule is sorted (non-decreasing), we can bucket and process in a single pass.
            // We round UP to the end of the window so we never run earlier than requested.
            var bucketTimeSpanTicks = toleranceTimeSpanTicks == 0
                ? offsetTimeSpanTicks
                : BucketCeiling(offsetTimeSpanTicks, toleranceTimeSpanTicks); // never runs early

            // When we enter a new bucket, we wait until that bucket's time has arrived.
            // After that, all items in the same bucket can be posted immediately (they're "due").
            if (currentBatchBucketTicks is null || bucketTimeSpanTicks != currentBatchBucketTicks.Value)
            {
                currentBatchBucketTicks = bucketTimeSpanTicks;

                await DelayUntilAsync(
                        startStopwatchTimestamp,
                        TimeSpan.FromTicks(bucketTimeSpanTicks),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // Post the work item to the ActionBlock.
            // If bounded capacity is set and the queue is full, this awaits until there's room (backpressure).
            await executionBlock.SendAsync(
                    new ScheduledItem(index, offsetTimeSpanTicks, bucketTimeSpanTicks),
                    cancellationToken)
                .ConfigureAwait(false);

            index = checked(index + 1);
        }
    }

    private static async Task RunUnsortedMaterializeAndSortAsync(
        IEnumerable<TimeSpan> schedule,
        ITargetBlock<ScheduledItem> executionBlock,
        long startStopwatchTimestamp,
        RatedScheduleRunnerSettings settings,
        CancellationToken cancellationToken)
    {
        var toleranceTimeSpanTicks = settings.BatchTolerance.Ticks;

        // If we know the count, pre-size the list to reduce reallocations.
        var scheduledItems = schedule is ICollection<TimeSpan> c
            ? new List<ScheduledItem>(c.Count)
            : new List<ScheduledItem>();

        // First pass: convert offsets into ScheduledItems and compute their bucket.
        var index = 0;
        foreach (var offset in schedule)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var offsetTimeSpanTicks = offset.Ticks;
            if (offsetTimeSpanTicks < 0)
            {
                if (!settings.SkipNegativeOffsets)
                {
                    throw new ArgumentOutOfRangeException(nameof(schedule),
                        $"Offset at index {index} is negative: {offset}");
                }

                index = checked(index + 1);
                continue;
            }

            // Round UP to the bucket boundary so we never run early.
            var bucketTimeSpanTicks = toleranceTimeSpanTicks == 0
                ? offsetTimeSpanTicks
                : BucketCeiling(offsetTimeSpanTicks, toleranceTimeSpanTicks); // never runs early

            scheduledItems.Add(new ScheduledItem(index, offsetTimeSpanTicks, bucketTimeSpanTicks));
            index = checked(index + 1);
        }

        // Now we sort, so we can:
        // - wait once per bucket in time order
        // - then post all items that belong to that bucket
        scheduledItems.Sort(ScheduledItemComparer);

        var position = 0;
        while (position < scheduledItems.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The bucket we are about to execute.
            var batchBucketTimeSpanTicks = scheduledItems[position].BucketTimeSpanTicks;

            // Wait until this bucket's time is reached relative to the start timestamp.
            await DelayUntilAsync(
                    startStopwatchTimestamp,
                    TimeSpan.FromTicks(batchBucketTimeSpanTicks),
                    cancellationToken)
                .ConfigureAwait(false);

            // Post every item in this bucket.
            while (position < scheduledItems.Count &&
                   scheduledItems[position].BucketTimeSpanTicks == batchBucketTimeSpanTicks)
            {
                await executionBlock.SendAsync(scheduledItems[position], cancellationToken)
                    .ConfigureAwait(false);

                position++;
            }
        }
    }

    private static RatedScheduleRunnerSettings Merge(
        RatedScheduleRunnerSettings defaults,
        int maxDegreeOfParallelism,
        int? boundedCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDegreeOfParallelism);

        if (boundedCapacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity));
        }

        // Clone defaults and override the chosen fields.
        // (If your settings type is immutable, replace with a "with" expression or factory.)
        return new RatedScheduleRunnerSettings
        {
            BatchTolerance = defaults.BatchTolerance,
            SkipNegativeOffsets = defaults.SkipNegativeOffsets,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            BoundedCapacity = boundedCapacity
        };
    }

    private static void ValidateEffectiveSettings(RatedScheduleRunnerSettings settings)
    {
        // Must be >= 1 worker.
        if (settings.MaxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.MaxDegreeOfParallelism));
        }

        // If set, bounded capacity must be >= 1 item.
        if (settings.BoundedCapacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.BoundedCapacity));
        }

        // Negative tolerance doesn't make sense.
        if (settings.BatchTolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.BatchTolerance));
        }
    }

    private static void ValidateStartStopwatchTimestamp(long startStopwatchTimestamp)
    {
        // Best-effort heuristic to catch obvious mistakes (e.g. DateTime.UtcNow.Ticks).
        // We allow large skew to avoid breaking long-running processes.
        var now = Stopwatch.GetTimestamp();
        var maxSkew = Stopwatch.Frequency * 60L * 60L * 24L * 365L * 5L; // 5 years of ticks

        var diff = startStopwatchTimestamp - now;
        if (diff > maxSkew || diff < -maxSkew)
        {
            throw new ArgumentOutOfRangeException(nameof(startStopwatchTimestamp),
                "startTimestamp must be a stopwatch timestamp from Stopwatch.GetTimestamp(). " +
                "It looks far outside the expected range relative to the current stopwatch timestamp.");
        }
    }

    /// <summary>
    /// Buckets to a tolerance window in a "never run early" way by rounding UP to the end of the window.
    /// This means items may be delayed up to the tolerance but won't execute before their requested offset.
    /// </summary>
    private static long BucketCeiling(long valueTimeSpanTicks, long toleranceTimeSpanTicks)
    {
        // Called only when tolerance > 0:
        // example with tolerance=10:
        // value=1..10  => 10
        // value=11..20 => 20
        var adjusted = checked(valueTimeSpanTicks + toleranceTimeSpanTicks - 1);
        return checked(adjusted / toleranceTimeSpanTicks * toleranceTimeSpanTicks);
    }

    /// <summary>
    /// Delays until the specified offset from the start stopwatch timestamp has elapsed.
    /// Uses coarse waiting for long delays and finer waiting as the target time approaches.
    /// </summary>
    private static async Task DelayUntilAsync(
        long startStopwatchTimestamp,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            // If the app is stopping or the caller cancelled, stop waiting immediately.
            cancellationToken.ThrowIfCancellationRequested();

            // How long since we started, based on a monotonic clock.
            var elapsed = Stopwatch.GetElapsedTime(startStopwatchTimestamp);

            // How much longer until we reach the desired offset.
            var remaining = offset - elapsed;

            // If we're past the target time, we're done waiting.
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Sleep in larger chunks when far away, then smaller chunks near the target.
            // This keeps CPU usage low without oversleeping by a lot.
            var sleep =
                remaining > TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(15) :
                remaining > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(1) :
                remaining;

            await Task.Delay(sleep, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Represents an item in a scheduled execution plan.
    /// </summary>
    private readonly record struct ScheduledItem(int Index, long OffsetTimeSpanTicks, long BucketTimeSpanTicks);

    private sealed class ScheduledItemBucketThenIndexComparer : IComparer<ScheduledItem>
    {
        public int Compare(ScheduledItem scheduledItemA, ScheduledItem scheduledItemB)
        {
            var comparisonResult = scheduledItemA.BucketTimeSpanTicks.CompareTo(scheduledItemB.BucketTimeSpanTicks);

            // If buckets differ, earlier bucket comes first. If same bucket, preserve input order by index.
            return comparisonResult != 0
                ? comparisonResult
                : scheduledItemA.Index.CompareTo(scheduledItemB.Index);
        }
    }
}
