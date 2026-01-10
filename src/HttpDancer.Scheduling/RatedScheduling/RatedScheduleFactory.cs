namespace HttpDancer.Scheduling.RatedScheduling;

/// <summary>
/// Produces time-based execution plans for a fixed number of actions,
/// based on <see cref="RatedScheduleOptions"/>.
/// </summary>
public sealed class RatedScheduleFactory(RatedScheduleRunnerSettings? ratedScheduleRunnerSettings = null) : IRatedScheduleFactory
{
    /// <inheritdoc />
    public RatedSchedule Create()
    {
        return Create(ratedScheduleRunnerSettings?.DefaultOptions ?? new RatedScheduleOptions());
    }
    
    /// <inheritdoc />
    public RatedSchedule Create(RatedScheduleOptions options)
    {
        var offsets = GenerateOffsets(options);
        var duration = CalculateDuration(options);
        return new RatedSchedule
        {
            Capacity = options.Capacity,
            Options = options, 
            Duration = duration, 
            Offsets = offsets.ToList()
        };
    }

    /// <summary>
    /// Generates a sequence of offsets (TimeSpan) indicating when each action
    /// should occur relative to a schedule start (t=0).
    ///
    /// Behavior:
    /// - Uses a token-bucket model.
    /// - Rate is interpreted as "Actions per Window".
    /// - If <see cref="RatedScheduleOptions.MinDuration"/> is provided
    ///   and large enough, the schedule is stretched to fill it.
    /// - The sequence is lazy and evaluated on enumeration.
    /// </summary>
    private IEnumerable<TimeSpan> GenerateOffsets(RatedScheduleOptions options)
    {
        if (options.Capacity <= 0)
        {
            yield break;
        }
        
        // Normalize inputs to safe minimums
        var actionsPerSpan = Math.Max(1, options.Rate);
        var span = options.RateWindow <= TimeSpan.Zero
            ? TimeSpan.FromTicks(1)
            : options.RateWindow;

        // Convert "actions per time span" into a continuous refill rate (actions/sec)
        var ratePerSecond = actionsPerSpan / span.TotalSeconds;

        // Compute minimum-time needed to execute all actions under rate-limiting
        var minRequiredSeconds = options.Capacity / ratePerSecond;

        // Determine the scaling-factor for stretching the schedule
        var scale = 1.0d;
        if (options.MinDuration is { } total &&
            total.TotalSeconds >= minRequiredSeconds &&
            minRequiredSeconds > 0)
        {
            scale = total.TotalSeconds / minRequiredSeconds;
        }

        // Token bucket state:
        // - tokens: available execution permits
        // - timeSeconds: current logical time (seconds since start)
        // - lastUpdateSeconds: last time tokens were refilled
        var tokens = 1.0;
        var timeSeconds = 0.0;
        var lastUpdateSeconds = 0.0;

        for (var i = 0; i < options.Capacity; i++)
        {
            // Refill tokens based on elapsed time since the last update
            var delta = timeSeconds - lastUpdateSeconds;
            if (delta > 0)
            {
                tokens += delta * ratePerSecond;
            }

            lastUpdateSeconds = timeSeconds;

            // If insufficient tokens, advance time until one token is available
            if (tokens < 1.0)
            {
                var wait = (1.0 - tokens) / ratePerSecond;

                timeSeconds += wait;

                // Refill tokens after waiting
                delta = timeSeconds - lastUpdateSeconds;
                if (delta > 0)
                {
                    tokens += delta * ratePerSecond;
                }

                lastUpdateSeconds = timeSeconds;
            }

            // Consume one token (guarding against floating-point drift)
            tokens = Math.Max(tokens, 1.0) - 1.0;

            // Emit the scheduled execution time, applying the scale-factor if any.
            yield return TimeSpan.FromSeconds(timeSeconds * scale);
        }
    }
    
    /// <summary>
    /// Calculates the total duration of a rated schedule request.
    /// Using the same rate-limiting and stretching rules as <see cref="GenerateOffsets(RatedScheduleOptions?)"/>.
    /// </summary>
    private TimeSpan CalculateDuration(RatedScheduleOptions options)
    {
        if (options.Capacity <= 0)
        {
            return TimeSpan.Zero;
        }
        
        // Normalize inputs to safe minimums
        var actionsPerSpan = Math.Max(1, options.Rate);
        var span = options.RateWindow <= TimeSpan.Zero
            ? TimeSpan.FromTicks(1)
            : options.RateWindow;

        // Convert "actions per time span" into a continuous refill rate (actions/sec)
        var ratePerSecond = actionsPerSpan / span.TotalSeconds;

        // Minimum time needed under rate-limiting
        var minRequiredSeconds = options.Capacity / ratePerSecond;

        if (minRequiredSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        // If a valid schedule duration is provided and is large enough,
        // the schedule is stretched to exactly fill it.
        if (options.MinDuration is { } total &&
            total.TotalSeconds >= minRequiredSeconds)
        {
            return total;
        }

        return TimeSpan.FromSeconds(minRequiredSeconds);
    }
}
