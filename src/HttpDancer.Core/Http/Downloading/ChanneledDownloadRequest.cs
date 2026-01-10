namespace HttpDancer.Core.Http.Downloading;

using HttpDancer.Core.Http.Clients;

/// <summary>
/// Holds configuration and callbacks for a download operation.
/// </summary>
public class ChanneledDownloadRequest
{
    /// <summary>
    /// The set of URLs to be downloaded (required for initialization)
    /// </summary>
    public required string[] Urls { get; set; }

    /// <summary>
    /// The maximum number of requests to perform in total before stopping
    /// </summary>
    public int MaxRequests { get; set; } = 10;

    /// <summary>
    /// The maximum number of downloads that can happen in parallel
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 2;

    /// <summary>
    /// Optional hook executed after dedupe but before scheduling a download.
    /// Return Proceed/SkipOnce/SkipPermanently to control flow.
    /// </summary>
    public Func<string, Task<RequestDecision>>? RequestAsync { get; set; }

    /// <summary>
    /// Optional callback invoked after each completed download with richer payload.
    /// </summary>
    public Func<ChanneledDownloadService, DownloadResponse, Task>? ResponseAsync { get; set; }

    /// <summary>
    /// Optional callback invoked wen the current status of the download operation change.
    /// </summary>
    public Func<ChanneledDownloadService, StatusResponse, Task>? StatusAsync { get; set; }

    /// <summary>
    /// Optional callback to decide per-response whether to read the body, after headers/status are known.
    /// Receives response metadata without the body bytes. Return true to force reading, false to force skipping,
    /// null to fall back to the configured defaults. Applies to both success and non-success responses.
    /// </summary>
    public Func<CompletedHttpResponseMessage, Task<bool?>>? ShouldReadBodyAsync { get; set; }

}
