namespace HttpDancer.FileSystemDownloader.Data;

public interface IHtmlMetaTagProvider
{
    Task<HtmlMetaTag[]> GetAsync(string utf8EncodedHtmlString, CancellationToken cancellationToken);
}
