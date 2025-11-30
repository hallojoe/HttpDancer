namespace HttpDancer.Core.Http.Clients;

public interface IHttpClient
{
    Task<ResponseMessage> GetAsync(string url, CancellationToken cancellationToken);
    Task<ResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken);
    Task<ResponseMessage> SendAsync(RequestMessage requestMessage, CancellationToken cancellationToken);
}
