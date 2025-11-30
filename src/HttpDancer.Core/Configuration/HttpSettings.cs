using HttpDancer.Core.Models;

namespace HttpDancer.Core.Configuration;

public partial class HttpSettings
{
    public const string Key = "Http";

    public bool ReadBodyOnNonSuccess { get; set; } = true;

    public HttpClientSettings HttpClient { get; set; } = new();
    public SocketsHttpHandlerSettings SocketsHttpHandler { get; set; } = new();
}