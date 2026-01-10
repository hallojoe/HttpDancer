using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// An enhanced runner for large schedules that:
/// - Uses <see cref="Stopwatch"/> for monotonic timing (avoids TickCount wrap)
/// - Batches offsets that fall within a tolerance window so "equal-ish" entries run together
/// - Validates inputs (rejects negative offsets, invalid capacities) and avoids precision loss
/// </summary>
public sealed class RatedScheduleRunner(RatedScheduleRunnerSettings? ratedScheduleRunnerSettings = null)
    : IRatedScheduleRunner
{
    private readonly RatedScheduleRunnerSettings _ratedScheduleRunnerSettings = 
        ratedScheduleRunnerSettings ?? new();

    /// <inheritdoc />
    public async Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        RatedScheduleRunnerSettings settings,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(
            schedule, 
            executeAsync, 
            startTimestamp, 
            settings.MaxDegreeOfParallelism, 
            settings.BoundedCapacity, 
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(
            schedule, 
            executeAsync, 
            startTimestamp, 
            _ratedScheduleRunnerSettings.MaxDegreeOfParallelism, 
            _ratedScheduleRunnerSettings.BoundedCapacity, 
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        int maxDegreeOfParallelism = 100,
        int? boundedCapacity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(executeAsync);
        
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }

        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity));
        }

        // Use Stopwatch ticks for monotonic time; caller-provided startTimestamp is assumed to be in the same units.
        var effectiveStartTimestamp = startTimestamp ?? Stopwatch.GetTimestamp();

        // Materialize schedule: keep both original offset (for callback) and bucketed offset (for grouping).
        var scheduledItems = new List<ScheduledItem>();
        var toleranceTicks = _ratedScheduleRunnerSettings.BatchTolerance.Ticks;

        var index = 0;
        foreach (var offset in schedule)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var offsetTicks = offset.Ticks;
            if (offsetTicks < 0)
            {
                if (!_ratedScheduleRunnerSettings.SkipNegativeOffsets)
                {
                    throw new ArgumentOutOfRangeException(nameof(schedule),
                        $"Offset at index {index} is negative: {offset}");
                }

                index = checked(index + 1);

                continue;
            }

            var bucketTicks = toleranceTicks == 0
                ? offsetTicks
                : RoundToNearest(offsetTicks, toleranceTicks);

            scheduledItems.Add(new ScheduledItem(index, offsetTicks, bucketTicks));

            index = checked(index + 1);
        }

        // Sort by bucketed time, then original index for deterministic ordering.
        scheduledItems.Sort((left, right) =>
        {
            var comparison = left.BucketTicks.CompareTo(right.BucketTicks);
            return comparison != 0
                ? comparison
                : left.Index.CompareTo(right.Index);
        });

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            EnsureOrdered = false
        };

        if (boundedCapacity is { } capacityValue)
        {
            executionOptions.BoundedCapacity = capacityValue;
        }

        // Use a single block for all scheduled items to avoid excessive concurrency and memory overhead.
        var executionBlock = new ActionBlock<ScheduledItem>(
            async item =>
            {
                var offset = TimeSpan.FromTicks(item.OffsetTicks);
                await executeAsync(item.Index, offset, cancellationToken).ConfigureAwait(false);
            },
            executionOptions);

        // Send items to the execution block in batches, grouped by bucketed time.
        var position = 0;
        while (position < scheduledItems.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchBucketTicks = scheduledItems[position].BucketTicks;

            await DelayUntilAsync(effectiveStartTimestamp, batchBucketTicks, cancellationToken).ConfigureAwait(false);

            // Send all items in the current batch to the execution block.
            while (position < scheduledItems.Count &&
                   scheduledItems[position].BucketTicks == batchBucketTicks)
            {
                // Asynchronously offers a message to the target message block, allowing for postponement.
                // SendAsync is non-blocking and will return immediately if the block is full.
                await executionBlock.SendAsync(scheduledItems[position], cancellationToken).ConfigureAwait(false);
                
                position++;
            }
        }

        // Signal completion to allow any remaining work to complete.
        executionBlock.Complete();
        
        // Wait for all work to complete before returning the effective start timestamp.
        await executionBlock.Completion.ConfigureAwait(false);

        return effectiveStartTimestamp;
    }

    private static long RoundToNearest(long valueTicks, long toleranceTicks)
    {
        // Round to the nearest multiple of toleranceTicks, favoring larger buckets on ties for determinism.
        var half = toleranceTicks / 2;
        var adjusted = checked(valueTicks + half);
        return checked(adjusted / toleranceTicks * toleranceTicks);
    }

    /// <summary>
    /// Delays the execution until the specified offset time from the start timestamp
    /// has elapsed, allowing for efficient coarse waiting for long delays and finer waiting as the target time approaches.
    /// </summary>
    /// <param name="startTimestamp">The starting timestamp, typically as a monotonic time reference in ticks.</param>
    /// <param name="offsetTicks">The offset in ticks from the starting timestamp at which the delay should complete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests during the delay operation.</param>
    /// <returns>A task that represents the asynchronous delay operation.</returns>
    private static async Task DelayUntilAsync(long startTimestamp, long offsetTicks,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            var remaining = TimeSpan.FromTicks(checked(offsetTicks - elapsed.Ticks));

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Coarse waits for long delays and tighten as the deadline approaches to avoid busy waiting.
            var sleep =
                remaining > TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(15) :
                remaining > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(1) :
                remaining;

            await Task.Delay(sleep, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Represents an item in a scheduled execution plan.
    /// Contains details about the item's position in the schedule,
    /// its specific time offset, and a bucketed offset for grouping similar execution times.
    /// </summary>
    /// <param name="Index">
    /// The zero-based index of the item in the schedule, used to maintain original order in case of ties.
    /// </param>
    /// <param name="OffsetTicks">
    /// The precise time offset, in ticks, from the start of the schedule to when this item is intended to execute.
    /// </param>
    /// <param name="BucketTicks">
    /// The rounded or grouped time offset, in ticks, based on a tolerance for consolidating executions that occur within a similar time frame.
    /// </param>
    private readonly record struct ScheduledItem(int Index, long OffsetTicks, long BucketTicks);
}
