namespace HttpDancer.Core.Http;

public interface IDownloadService
{
    /// <summary>
    /// Public API to enqueue a single URL (does not modify DownloadContext).
    /// Returns true if the URL was accepted into the queue.
    /// </summary>
    bool EnqueueUrl(string? url);

    /// <summary>
    /// Public API to enqueue multiple URLs (does not modify DownloadContext).
    /// Returns the number of accepted URLs.
    /// </summary>
    int EnqueueUrls(IEnumerable<string?>? urls);

    /// <summary>
    /// Drains the queue and processes downloads while respecting limits.
    /// The loop also idles briefly to allow dynamic producers to enqueue more work.
    /// </summary>
    Task<bool> DownloadAsync(CancellationToken cancellationToken = default);
}