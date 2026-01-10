namespace HttpDancer.Scheduling.RatedScheduling;

internal static class RatedScheduleExtensions
{
    /// <summary>
    /// Returns all offsets that fall within the next <paramref name="window"/>
    /// relative to the already elapsed time.
    /// Example: elapsed = 2s, window = 5s returns offsets in (2s, 7s].
    /// </summary>
    public static IEnumerable<TimeSpan> SliceNext(
        this IEnumerable<TimeSpan> schedule,
        TimeSpan elapsed,
        TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentOutOfRangeException.ThrowIfLessThan(window, TimeSpan.Zero);

        var windowEnd = elapsed + window;

        foreach (var offset in schedule)
        {
            if (offset <= elapsed)
            {
                continue;
            }

            if (offset > windowEnd)
            {
                yield break;
            }

            yield return offset;
        }
    }
}