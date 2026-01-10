namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Defines a contract for creating scheduled execution plans with
/// time-based intervals and duration, adhering to specified options.
/// </summary>
public interface IRatedScheduleFactory
{
    /// <summary>
    /// Creates a new rated schedule response based on the provided options, which includes
    /// calculated time offsets and the total duration of the schedule.
    /// </summary>
    /// <param name="options">The configuration for the schedule, including capacity,
    /// rate, rate window, and optional minimum duration.</param>
    /// <returns>A <see cref="RatedSchedule"/> containing the original options,
    /// the schedule duration, and the sequence of calculated time offsets.</returns>
    RatedSchedule Create(RatedScheduleOptions options);

    /// <summary>
    /// Creates a new rated schedule response based on the option defaults, which includes
    /// calculated time offsets and the total duration of the schedule are all default values.
    /// </summary>
    RatedSchedule Create();

}