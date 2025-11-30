namespace HttpDancer.Core;

public interface IHttpClient
{
    Task<ResourceResponse> GetAsync(string url, CancellationToken cancellationToken);
    Task<ResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken);
}
