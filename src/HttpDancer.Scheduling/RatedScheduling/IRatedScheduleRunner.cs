namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Executes asynchronous callbacks according to a sequence of time offsets (relative schedule).
///
/// Offsets are interpreted as elapsed time from a monotonic start timestamp, using a monotonic clock
/// to avoid issues caused by wall-clock changes (DST, NTP adjustments, manual time updates).
///
/// Implementations are intended to support large schedules efficiently (tens of thousands of offsets),
/// including scenarios where many offsets are identical.
/// </summary>
public interface IRatedScheduleRunner
{
    /// <summary>
    /// Executes <paramref name="executeAsync"/> once for each offset in <paramref name="schedule"/>,
    /// relative to a monotonic start timestamp.
    ///
    /// Offsets represent elapsed time from the moment execution begins, or from <paramref name="startTimestamp"/>
    /// if supplied. Items scheduled for the same offset become eligible at the same time and may execute
    /// concurrently, subject to <paramref name="maxDegreeOfParallelism"/>.
    ///
    /// Enumeration order determines the callback index. Negative offsets are skipped but still consume an index.
    ///
    /// If execution falls behind schedule, overdue offsets are executed without additional delay.
    ///
    /// The method returns the effective start timestamp used for scheduling, which can be reused for
    /// progress reporting, slicing, or resuming execution.
    /// </summary>
    /// <param name="schedule">
    /// The collection of time offsets to execute relative to the effective start timestamp.
    /// Each element represents an elapsed duration from the start time at which the corresponding callback becomes eligible.
    /// </param>
    /// <param name="executeAsync">
    /// The callback to execute for each enumerated offset. The callback receives:
    /// <list type="bullet">
    ///   <item><description><c>index</c>: the enumeration index of the offset (increments for every offset, even if skipped)</description></item>
    ///   <item><description><c>offset</c>: the scheduled offset that determined eligibility timing</description></item>
    ///   <item><description><c>cancellationToken</c>: a token that should be observed by the callback</description></item>
    /// </list>
    /// </param>
    /// <param name="startTimestamp">
    /// Optional monotonic start timestamp used as the reference point for all offsets.
    /// If not supplied, the current monotonic timestamp is captured at the start of execution.
    /// </param>
    /// <param name="maxDegreeOfParallelism">
    /// The maximum number of callbacks that may run concurrently when multiple offsets are eligible at the same time
    /// (for example, identical offsets or overdue work).
    /// </param>
    /// <param name="boundedCapacity">
    /// Optional bounded queue capacity for the underlying executor. When specified, posting work may apply backpressure
    /// if the executor cannot keep up with the schedule.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel waiting and execution. Implementations should observe this token while delaying and
    /// should pass it through to <paramref name="executeAsync"/>.
    /// </param>
    /// <returns>
    /// The effective monotonic start timestamp used for interpreting offsets.
    /// </returns>
    Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        int maxDegreeOfParallelism = 100,
        int? boundedCapacity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="executeAsync"/> once for each offset in <paramref name="schedule"/>,
    /// relative to a monotonic start timestamp, using implementation defaults for execution settings.
    ///
    /// Enumeration order determines the callback index. Negative offsets are skipped but still consume an index.
    /// If execution falls behind schedule, overdue offsets are executed without additional delay.
    ///
    /// The method returns the effective start timestamp used for scheduling, which can be reused for
    /// progress reporting, slicing, or resuming execution.
    /// </summary>
    /// <param name="schedule">
    /// The collection of time offsets to execute relative to the effective start timestamp.
    /// Each element represents an elapsed duration from the start time at which the corresponding callback becomes eligible.
    /// </param>
    /// <param name="executeAsync">
    /// The callback to execute for each enumerated offset. The callback receives the enumeration index, the offset,
    /// and a cancellation token.
    /// </param>
    /// <param name="startTimestamp">
    /// Optional monotonic start timestamp used as the reference point for all offsets.
    /// If not supplied, the current monotonic timestamp is captured at the start of execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel waiting and execution. Implementations should observe this token while delaying and
    /// should pass it through to <paramref name="executeAsync"/>.
    /// </param>
    /// <returns>
    /// The effective monotonic start timestamp used for interpreting offsets.
    /// </returns>
    Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="executeAsync"/> once for each offset in <paramref name="schedule"/>,
    /// relative to a monotonic start timestamp, using the provided <paramref name="settings"/>.
    ///
    /// Enumeration order determines the callback index. Negative offsets are skipped but still consume an index.
    /// If execution falls behind schedule, overdue offsets are executed without additional delay.
    ///
    /// The method returns the effective start timestamp used for scheduling, which can be reused for
    /// progress reporting, slicing, or resuming execution.
    /// </summary>
    /// <param name="schedule">
    /// The collection of time offsets to execute relative to the effective start timestamp.
    /// Each element represents an elapsed duration from the start time at which the corresponding callback becomes eligible.
    /// </param>
    /// <param name="executeAsync">
    /// The callback to execute for each enumerated offset. The callback receives the enumeration index, the offset,
    /// and a cancellation token.
    /// </param>
    /// <param name="settings">
    /// Runner settings controlling execution behavior such as concurrency limits and queue capacity.
    /// </param>
    /// <param name="startTimestamp">
    /// Optional monotonic start timestamp used as the reference point for all offsets.
    /// If not supplied, the current monotonic timestamp is captured at the start of execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel waiting and execution. Implementations should observe this token while delaying and
    /// should pass it through to <paramref name="executeAsync"/>.
    /// </param>
    /// <returns>
    /// The effective monotonic start timestamp used for interpreting offsets.
    /// </returns>
    Task<long> RunAsync(
        IEnumerable<TimeSpan> schedule,
        Func<int, TimeSpan, CancellationToken, Task> executeAsync,
        RatedScheduleRunnerSettings settings,
        long? startTimestamp = null,
        CancellationToken cancellationToken = default);
}
