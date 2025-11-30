using System.Net;
using System.Text.Json.Serialization;

namespace HttpDancer.Core.Models;

public sealed class SocketsHttpHandlerSettings
{
    /// <summary>
    /// Maximum concurrent TCP connections allowed per (scheme, host, port).
    /// Default: 20.
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 20;

    /// <summary>
    /// Forces pooled connections to be closed and recreated after the specified lifetime.
    /// Default: 1 minute.
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum time an idle pooled connection is allowed to remain unused.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan PooledConnectionIdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Automatic decompression options for HTTP responses.
    /// Default: GZip + Deflate.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DecompressionMethods AutomaticDecompression { get; set; } =
        DecompressionMethods.GZip | DecompressionMethods.Deflate;

    /// <summary>
    /// Time allowed for establishing TCP & TLS connections (connect handshake).
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Allows more than one HTTP/2 connection per server.
    /// Default: true.
    /// </summary>
    public bool EnableMultipleHttp2Connections { get; set; } = true;

    /// <summary>
    /// Controls whether 3xx redirects are automatically followed.
    /// Default: false.
    /// </summary>
    public bool AllowAutoRedirect { get; set; } = false;

    /// <summary>
    /// Enables or disables automatic cookie storage and sending.
    /// Default: false.
    /// </summary>
    public bool UseCookies { get; set; } = false;

    /// <summary>
    /// Delay before sending keep-alive pings on HTTP/2 & HTTP/3 connections.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan KeepAlivePingDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time to wait for a keep-alive ping response.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan KeepAlivePingTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Defines when HTTP/2/3 keep-alive pings are sent.
    /// Default: Always.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HttpKeepAlivePingPolicy KeepAlivePingPolicy { get; set; } =
        HttpKeepAlivePingPolicy.Always;
}