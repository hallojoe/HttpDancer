namespace HttpDancer.Core.Http.Configuration;

public class HttpClientSettings
{
    /// <summary>
    /// Total allowed duration for the entire HTTP request lifecycle.
    /// Default: 20 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// User-Agent header value sent with all outgoing requests.
    /// Default: Modern Chrome desktop UA.
    /// </summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";
}