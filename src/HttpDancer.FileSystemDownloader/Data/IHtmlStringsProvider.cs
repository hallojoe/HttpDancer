namespace HttpDancer.FileSystemDownloader.Data;

public interface IHtmlStringsProvider
{
    Task<Dictionary<string, object?>> GetAsync(string utf8EncodedHtmlString, CancellationToken cancellationToken);
}