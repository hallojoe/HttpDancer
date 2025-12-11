namespace HttpDancer.FileSystemDownloader.Data;

public interface IHtmlStringsProvider
{
    Task<Dictionary<string, string?>> GetAsync(string utf8EncodedHtmlString, CancellationToken cancellationToken);
}