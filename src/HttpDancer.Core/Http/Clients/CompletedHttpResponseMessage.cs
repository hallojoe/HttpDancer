using System.Net;
using System.Text.Json.Serialization;

namespace HttpDancer.Core.Http.Clients;

[Serializable]
public abstract class HttpResponseMessageBase
{
    /// <summary>
    /// The final resolved URL of the resource (after redirects, if applicable).
    /// </summary>
    public Uri? Uri { get; init; }

    /// <summary>
    /// Convenience URL.
    /// </summary>
    public string?  Url => Uri?.ToString();

    /// <summary>
    /// 
    /// </summary>
    public required RequestMessage Request { get; set; }
    
    /// <summary>
    /// HTTP method used to fetch the resource.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// Convenience flag equivalent to HttpResponseMessage.IsSuccessStatusCode.
    /// </summary>
    public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;

    /// <summary>
    /// The Content-Type header value, if present.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Value headers (case insensitive, supports multi-value headers).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Headers { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Claimed body length.
    /// </summary>
    public long? BodyLength { get; set; }

    /// <summary>
    /// Optional textual message (e.g. error details, validation message).
    /// Intended for humans, not protocol-level data.
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// Correlation id associated with the request/response.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Represents an HTTP response message that has been started but not yet completed.
/// </summary>
public sealed class StartedHttpResponseMessage : HttpResponseMessageBase { }

/// <summary>
/// Represent a completed HTTP response message.
/// </summary>
public sealed class CompletedHttpResponseMessage : HttpResponseMessageBase
{
    public CompletedHttpResponseMessage() { }

    public CompletedHttpResponseMessage(HttpResponseMessageBase source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // init-only properties on the base can still be assigned here (inside ctor).
        Uri = source.Uri;
        Method = source.Method;
        StatusCode = source.StatusCode;
        ContentType = source.ContentType;
        BodyLength = source.BodyLength;
        Message = source.Message;
        CorrelationId = source.CorrelationId;
        Request = source.Request;
        Headers = source.Headers is null
            ? null
            : source.Headers.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        // If caller passes an already-completed message, preserve its completion time.
        CompletionDateTime = source is CompletedHttpResponseMessage completed
            ? completed.CompletionDateTime
            : DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the date and time, in UTC, when the HTTP response was completed.
    /// </summary>
    public DateTime CompletionDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Raw response body as bytes. Null if there was no body or it was not read.
    /// </summary>
    [JsonIgnore]
    public byte[]? BodyBytes { get; set; }
}
