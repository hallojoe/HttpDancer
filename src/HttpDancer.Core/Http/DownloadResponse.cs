namespace HttpDancer.Core.Http;

/// <summary>
/// Wraps the result of a single download plus live counters.
/// </summary>
public sealed class DownloadResponse
{
    /// <summary>
    /// The URL processed
    /// </summary>
    public required string Url { get; init; }                  
    
    /// <summary>
    /// Underlying client response
    /// </summary>
    public required ResourceResponse Value { get; set; }   
}



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
}
