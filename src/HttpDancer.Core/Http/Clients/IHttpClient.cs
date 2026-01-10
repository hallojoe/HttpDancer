namespace HttpDancer.Core.Http.Clients;

public interface IHttpClient
{
    Task<ResponseMessage> GetAsync(string url, CancellationToken cancellationToken);

    Task<ResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken);
    
    Task<ResponseMessage> PostAsync(string url, HttpContent? content, CancellationToken cancellationToken);
    
    Task<ResponseMessage> PostAsync(Uri uri, HttpContent? content, CancellationToken cancellationToken);
    
    Task<ResponseMessage> HeadAsync(string url, CancellationToken cancellationToken);
    
    Task<ResponseMessage> HeadAsync(Uri uri, CancellationToken cancellationToken);

    Task<ResponseMessage> SendAsync(RequestMessage requestMessage, CancellationToken cancellationToken);
}
