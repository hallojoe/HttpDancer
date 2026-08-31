using HttpDancer.Core.Http.Configuration;

namespace HttpDancer.Core;

public class HttpDancerSettings 
{
    public const string Key = "HttpDancer";
    public bool Enabled { get; set; } = true;
    public HttpDancerClientSettings DefaultClient { get; set; } = new();
    public Dictionary<string, HttpDancerClientSettings> NamedClients { get; set; } = [];
}

public class HttpDancerClientSettings 
{
    /// <summary>
    /// Specifies the content types that are allowed for processing or downloading.
    /// </summary>
    public string AllowedContentTypes { get; set; } = "text/*";

    /// <summary>
    /// Comma-separated wildcard patterns for URLs that must not be included in a download manifest.
    /// This is applied before download scheduling; it does not affect the seed request.
    /// </summary>
    public string? DisallowedUrlPattern { get; set; }

    /// <summary>
    /// Specifies the permitted host patterns for HTTP requests.
    /// Hosts matching this pattern can proceed, while others are skipped.
    /// Wildcard patterns are supported for flexible matching.
    /// </summary>
    public string AllowedHosts { get; set; } = "*";

    /// <summary>
    /// Determines whether the response body is read when the HTTP request completes successfully.
    /// </summary>
    public bool? ReadBodyOnSuccess { get; set; } = null;

    /// <summary>
    /// Specifies whether the response body should be read when the HTTP request does not complete successfully.
    /// </summary>
    public bool? ReadBodyOnNonSuccess { get; set; } = null;

    /// <summary>
    /// Whether to include a correlation id header on outgoing HTTP requests.
    /// </summary>
    public bool IncludeCorrelationIdHeader { get; set; } = true;
    
    /// <summary>
    /// The HTTP header name used for correlation id propagation.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// Specifies the maximum number of concurrent requests that can be processed simultaneously.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 2;

    /// <summary>
    /// Specifies the maximum number of requests that can be processed.
    /// </summary>
    public int MaxRequests { get; set; } = 1000;

    /// <summary>
    /// Represents the settings for configuring an HTTP client, including options such as timeout and user-agent.
    /// </summary>
    public HttpClientSettings HttpClient { get; set; } = new();

    /// <summary>
    /// Provides a configurable property for customizing the behavior of System.Net.Http.SocketsHttpHandler
    /// by using the associated SocketsHttpHandlerSettings.
    /// </summary>
    /// <remarks>
    /// This property allows you to define specific handler configurations, such as connection pooling,
    /// automatic decompression, timeouts, and other advanced HTTP connection behavior. It acts as a
    /// central point for customizing the HTTP client's underlying transport behavior within the application.
    /// </remarks>
    public SocketsHttpHandlerSettings SocketsHttpHandler { get; set; } = new();
}
