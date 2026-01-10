using System.Net.Http.Headers;

namespace HttpDancer.Core.Http.Clients;

public interface IHttpClient
{
    Task<CompletedHttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> PostAsync(string url, HttpContent? content, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> PostAsync(Uri uri, HttpContent? content, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> HeadAsync(string url, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> HeadAsync(Uri uri, CancellationToken cancellationToken);
    
    Task<CompletedHttpResponseMessage> SendAsync(RequestMessage requestMessage, CancellationToken cancellationToken);
    
    // Task<CompletedHttpResponseMessage> SendAsync(HttpMethod httpMethod, Uri uri, HttpHeaders? httpHeaders, HttpContent? httpContent, CancellationToken? cancellationToken);
    //
    // Task<CompletedHttpResponseMessage> SendAsync(
    //     HttpMethod httpMethod, 
    //     Uri uri, 
    //     HttpHeaders? httpHeaders, 
    //     HttpContentHeaders? httpContentHeaders, 
    //     HttpContent? httpContent, 
    //     CancellationToken? cancellationToken);

}
