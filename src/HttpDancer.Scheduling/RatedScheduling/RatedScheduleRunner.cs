using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// An enhanced runner for large schedules that:
/// - Uses <see cref="Stopwatch"/> for monotonic timing
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
    private static readonly IComparer<ScheduledItem> ScheduledItemComparer = new ScheduledItemBucketThenIndexComparer();

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

        ValidateEffectiveSettings(effectiveSettings);

        // Stopwatch timestamp only.
        var effectiveStartStopwatchTimestamp = startStopwatchTimestamp ?? Stopwatch.GetTimestamp();
        ValidateStartStopwatchTimestamp(effectiveStartStopwatchTimestamp);

        var executionOptions = new ExecutionDataflowBlockOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = effectiveSettings.MaxDegreeOfParallelism,
            EnsureOrdered = false
        };

        if (effectiveSettings.BoundedCapacity is { } cap)
        {
            executionOptions.BoundedCapacity = cap;
        }

        var executionBlock = new ActionBlock<ScheduledItem>(
            async item =>
            {
                var offset = TimeSpan.FromTicks(item.OffsetTimeSpanTicks);
                await executeAsync(item.Index, offset, cancellationToken).ConfigureAwait(false);
            },
            executionOptions);

        try
        {
            if (assumeSortedSchedule)
            {
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
                await RunUnsortedMaterializeAndSortAsync(
                        schedule,
                        executionBlock,
                        effectiveStartStopwatchTimestamp,
                        effectiveSettings,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            // Always complete the block so awaiting Completion won't hang.
            executionBlock.Complete();
        }

        await executionBlock.Completion.ConfigureAwait(false);
        return effectiveStartStopwatchTimestamp;
    }

    private static async Task RunSortedStreamingAsync(
        IEnumerable<TimeSpan> sortedSchedule,
        ITargetBlock<ScheduledItem> executionBlock,
        long startStopwatchTimestamp,
        RatedScheduleRunnerSettings settings,
        CancellationToken cancellationToken)
    {
        var toleranceTimeSpanTicks = settings.BatchTolerance.Ticks;

        var index = 0;
        long? currentBatchBucketTicks = null;

        foreach (var offset in sortedSchedule)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var offsetTimeSpanTicks = offset.Ticks;

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

            // Assumes offsets are non-decreasing.
            var bucketTimeSpanTicks = toleranceTimeSpanTicks == 0
                ? offsetTimeSpanTicks
                : BucketCeiling(offsetTimeSpanTicks, toleranceTimeSpanTicks); // never runs early

            if (currentBatchBucketTicks is null || bucketTimeSpanTicks != currentBatchBucketTicks.Value)
            {
                currentBatchBucketTicks = bucketTimeSpanTicks;
                await DelayUntilAsync(
                        startStopwatchTimestamp,
                        TimeSpan.FromTicks(bucketTimeSpanTicks),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // SendAsync can await when bounded capacity is reached (backpressure).
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

        // Pre-size list if we can (reduces allocations).
        var scheduledItems = schedule is ICollection<TimeSpan> c
            ? new List<ScheduledItem>(c.Count)
            : new List<ScheduledItem>();

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

            var bucketTimeSpanTicks = toleranceTimeSpanTicks == 0
                ? offsetTimeSpanTicks
                : BucketCeiling(offsetTimeSpanTicks, toleranceTimeSpanTicks); // never runs early

            scheduledItems.Add(new ScheduledItem(index, offsetTimeSpanTicks, bucketTimeSpanTicks));
            index = checked(index + 1);
        }

        // Sort by bucketed time, then original index for deterministic ordering.
        scheduledItems.Sort(ScheduledItemComparer);

        var position = 0;
        while (position < scheduledItems.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchBucketTimeSpanTicks = scheduledItems[position].BucketTimeSpanTicks;

            await DelayUntilAsync(
                    startStopwatchTimestamp,
                    TimeSpan.FromTicks(batchBucketTimeSpanTicks),
                    cancellationToken)
                .ConfigureAwait(false);

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

        // Minimal approach: clone defaults and override the two fields.
        // If your settings type is immutable, replace with a "with" expression or factory.
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
        if (settings.MaxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.MaxDegreeOfParallelism));
        }

        if (settings.BoundedCapacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.BoundedCapacity));
        }

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
        // tolerance must be > 0 when called
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
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = Stopwatch.GetElapsedTime(startStopwatchTimestamp);
            var remaining = offset - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

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
        public int Compare(ScheduledItem x, ScheduledItem y)
        {
            var c = x.BucketTimeSpanTicks.CompareTo(y.BucketTimeSpanTicks);
            return c != 0 ? c : x.Index.CompareTo(y.Index);
        }
    }
}
