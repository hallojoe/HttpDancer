namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Represents a rated schedule including its options,
/// the total duration, and a collection of time intervals that correspond to the scheduled values.
/// </summary>
public sealed class RatedSchedule
{
    /// <summary>
    /// Gets the rated schedule options used to produce this schedule.
    /// </summary>
    public RatedScheduleOptions? Options { get; init; }

    /// <summary>
    /// Numbers of time spans that the schedule should hold.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Gets the total duration of the rated schedule response, representing the time span covered by the scheduled values.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets the collection of time intervals corresponding to the scheduled values in the rated schedule response.
    /// </summary>
    public IReadOnlyList<TimeSpan> Offsets { get; init; } = [];
}