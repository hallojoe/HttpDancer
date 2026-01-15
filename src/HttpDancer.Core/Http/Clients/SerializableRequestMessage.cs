namespace HttpDancer.Core.Http.Clients;

public sealed class HttpPipelineOptions
{
    /// <summary>
    /// Optional override: whether to read the body for successful responses.
    /// If null, the client falls back to HttpDancerSettings.ReadBodyOnSuccess (except for HEAD).
    /// </summary>
    public bool? ReadBodyOnSuccess { get; init; }

    /// <summary>
    /// Optional override: whether to read the body for non-success responses.
    /// If null, the client falls back to HttpDancerSettings.ReadBodyOnNonSuccess (except for HEAD).
    /// </summary>
    public bool? ReadBodyOnNonSuccess { get; init; }

    /// <summary>
    /// Optional callback to decide whether to read the body after inspecting the response metadata
    /// (headers, status, etc). Return true to force read, false to skip, null to use defaults.
    /// </summary>
    public Func<HttpResponseMessage, Task<bool?>>? ShouldReadBodyAsync { get; init; }
}

[Serializable]
public sealed class SerializableRequestMessage
{
    /// <summary>
    /// Original URI for the request.
    /// </summary>
    // public required string OriginalUrl { get; init; }

    /// <summary>
    /// Target URI for the request.
    /// </summary>
    public required Uri Uri { get; init; }

    /// <summary>
    /// HTTP method to use. Defaults to GET.
    /// </summary>
    public HttpMethod Method { get; init; } = HttpMethod.Get;

    /// <summary>
    /// Optional HTTP content (body) for methods like POST/PUT/PATCH. 
    /// </summary>
    public HttpContent? Content { get; init; }

    /// <summary>
    /// Optional per-request headers to be added to the request.
    /// Keys are case-insensitive.
    /// </summary>
    public IDictionary<string, string?>? Headers { get; init; }
    
    /// <summary>
    /// Optional per-request content headers to be added to the request.
    /// Keys are case-insensitive.
    /// </summary>
    public IDictionary<string, string?>? ContentHeaders { get; init; }

    /// <summary>
    /// Optional override: whether to read the body for successful responses.
    /// If null, the client falls back to HttpDancerSettings.ReadBodyOnSuccess (except for HEAD).
    /// </summary>
    public bool? ReadBodyOnSuccess { get; init; }

    /// <summary>
    /// Optional override: whether to read the body for non-success responses.
    /// If null, the client falls back to HttpDancerSettings.ReadBodyOnNonSuccess (except for HEAD).
    /// </summary>
    public bool? ReadBodyOnNonSuccess { get; init; }

    /// <summary>
    /// Optional callback to decide whether to read the body after inspecting the response metadata
    /// (headers, status, etc). Return true to force read, false to skip, null to use defaults.
    /// </summary>
    public Func<HttpResponseMessage, Task<bool?>>? ShouldReadBodyAsync { get; init; }

    /// <summary>
    /// Optional per-request timeout. If set, the request will be canceled after this duration,
    /// independently of HttpClient.Timeout and the outer cancellation token.
    ///  
    /// </summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>
    /// Optional additional cancellation token for this specific request.
    /// This is combined with the outer token passed to the client.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Optional correlation id for this request.
    /// If not set, the client will prefer Activity.Current.TraceId, then fall back to a GUID.
    /// </summary>
    public string? CorrelationId { get; init; }}
