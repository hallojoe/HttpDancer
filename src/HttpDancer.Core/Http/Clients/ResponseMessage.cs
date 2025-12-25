using System.Net;
using System.Text.Json.Serialization;

namespace HttpDancer.Core.Http.Clients;


public sealed class ResponseMessage
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
    /// Raw response body as bytes. Null if there was no body or it was not read.
    /// </summary>
    [JsonIgnore]
    public byte[]? BodyBytes { get; set; }

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