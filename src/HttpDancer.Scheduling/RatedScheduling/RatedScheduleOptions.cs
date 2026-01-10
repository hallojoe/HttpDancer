namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Options for scheduling a fixed number of things over time, using a rate (token bucket–style) model.
/// Example: Thing can do 8 things every 45 minutes. That would translate to Rate = 8 things, RateWindow = 45 minutes. 
/// </summary>
public sealed class RatedScheduleOptions
{
    /// <summary>
    /// Numbers of time spans that the schedule should hold.
    /// </summary>
    public int Capacity { get; set; } = 8;

    /// <summary>
    /// Maximum number of things allowed within a single rate window(time span).
    /// </summary>
    public int Rate { get; set; } = 1;

    /// <summary>
    /// The duration of the rate window.
    /// Example: 2 actions per 1 second.
    /// </summary>
    public TimeSpan RateWindow { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Optional minimum duration of the entire schedule.
    /// If set and larger than the minimum required duration, the schedule
    /// is proportionally stretched to fill this period.
    /// </summary>
    public TimeSpan? MinDuration { get; set; }
}