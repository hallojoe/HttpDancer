namespace HttpDancer.Core.Http.Clients;

public interface IHttpClient
{
    Task<CompletedHttpResponseMessage> SendAsync(SerializableRequestMessage serializableRequestMessage, CancellationToken cancellationToken);
}
