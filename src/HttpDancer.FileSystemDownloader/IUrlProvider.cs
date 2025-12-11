namespace HttpDancer.FileSystemDownloader;

public interface IUrlProvider
{
    Task<string[]> GetUrlsAsync();
}