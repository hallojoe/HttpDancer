namespace HttpDancer.FileSystemDownloader;

public interface IUrlProvider
{
    /// <summary>
    /// Provides a list of URL's.
    /// </summary>
    /// <returns>String array of URL's.</returns>
    Task<string[]> GetUrlsAsync();
}