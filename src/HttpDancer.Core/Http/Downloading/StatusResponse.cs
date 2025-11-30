namespace HttpDancer.Core.Http.Downloading;

/// <summary>
/// Wraps the result of live counters.
/// </summary>
public sealed class StatusResponse
{
    /// <summary>
    /// Count of URLs marked processed so far
    /// </summary>
    public int ProcessedCount { get; init; }                   
    
    /// <summary>
    /// Estimated URLs left to start (bounded by MaxRequests)
    /// </summary>
    public int RemainingCount { get; init; }

    /// <summary>
    /// Items currently in-flight.
    /// </summary>
    public int InFlightCount { get; init; }

    /// <summary>
    /// Items waiting in the channel.
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Total requests that have been scheduled/attempted so far.
    /// </summary>
    public int ScheduledCount { get; init; }

    /// <summary>
    /// Max requests allowed for this run.
    /// </summary>
    public int MaxRequests { get; init; }
}