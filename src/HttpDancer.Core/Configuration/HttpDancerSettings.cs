using HttpDancer.Core.Http.Configuration;

namespace HttpDancer.Core.Configuration;

public class HttpDancerSettings 
{
    public const string Key = "HttpDancer";

    public bool? ReadBodyOnSuccess { get; set; } = null;

    public bool? ReadBodyOnNonSuccess { get; set; } = null;

    /// <summary>
    /// Whether to include a correlation id header on outgoing HTTP requests.
    /// </summary>
    public bool IncludeCorrelationIdHeader { get; set; } = true;
    
    /// <summary>
    /// The HTTP header name used for correlation id propagation.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";
    
    public int MaxConcurrentRequests { get; set; } = 2;

    public int MaxRequests { get; set; } = 1000;
    
    public HttpClientSettings HttpClient { get; set; } = new();
    
    public SocketsHttpHandlerSettings SocketsHttpHandler { get; set; } = new();

    public string AllowedContentTypes { get; set; } = "text/*";

    public string AllowedHosts { get; set; } = "*";
}