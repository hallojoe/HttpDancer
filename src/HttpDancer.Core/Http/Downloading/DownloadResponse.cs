using HttpDancer.Core.Http.Clients;

namespace HttpDancer.Core.Http.Downloading;

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
    public required ResponseMessage Value { get; set; }   
}