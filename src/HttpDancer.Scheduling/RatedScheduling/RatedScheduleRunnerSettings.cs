namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Represents the configuration settings for the RatedScheduleRunner,
/// used to control the behavior of the scheduling and execution process.
/// </summary>
public sealed class RatedScheduleRunnerSettings
{
    /// <summary>
    /// The configuration key used to bind settings for the RatedScheduleRunner.
    /// This key is associated with the configuration section that defines behavior
    /// and properties relevant to the RatedScheduleRunner.
    /// </summary>
    public const string Key = "RatedScheduleRunner";

    /// <summary>
    /// If non-zero, offsets that fall within this tolerance window are grouped together.
    /// This allows "equal-ish" entries to run together, which can improve throughput.
    /// For example, if BatchTolerance = 10s, offsets 10s, 20s, 30s will be grouped together.
    /// </summary>
    public TimeSpan BatchTolerance { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Determines whether negative offsets in the schedule should be skipped.
    /// If set to true, any negative offset encountered in the schedule will be ignored.
    /// If set to false, an <see cref="ArgumentOutOfRangeException"/> will be thrown
    /// for any negative offset encountered.
    /// </summary>
    public bool SkipNegativeOffsets { get; set; } = false;

    /// <summary>
    /// Specifies the maximum number of operations that can be executed concurrently during schedule execution.
    /// This setting enforces a limit on parallelism, ensuring system resources are managed efficiently while
    /// maintaining high throughput. A value less than or equal to zero will result in an exception.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 100;

    /// <summary>
    /// Represents the maximum number of items that can be queued in the processing pipeline at any given time.
    /// This property is used to limit memory usage by bounding the capacity of the internal pipeline. If set to null,
    /// the pipeline's capacity is considered unbounded.
    /// </summary>
    public int? BoundedCapacity { get; set; } = null;

    /// <summary>
    /// Represents the default scheduling options for the RatedScheduleRunner.
    /// These options define the rate-limiting behavior, such as capacity, rate,
    /// rate window, and other related configuration settings.
    /// </summary>
    public RatedScheduleOptions DefaultOptions { get; set; } = new();

}