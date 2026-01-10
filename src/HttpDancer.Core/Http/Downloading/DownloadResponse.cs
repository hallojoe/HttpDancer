using HttpDancer.Core.Http.Clients;

namespace HttpDancer.Core.Http.Downloading;

/// <summary>
/// Wraps the result of a single download plus live counters.
/// </summary>
public sealed class DownloadResponse
{
    /// <summary>
    /// Method used to download the URL
    /// </summary>
    public required string Method { get; init; }                  

    /// <summary>
    /// The URL processed
    /// </summary>
    public required string Url { get; init; }                  
    
    /// <summary>
    /// Underlying client response
    /// </summary>
    public required CompletedHttpResponseMessage Value { get; set; }   
}