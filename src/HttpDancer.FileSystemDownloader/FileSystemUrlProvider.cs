using HttpDancer.FileSystemDownloader.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.FileSystemDownloader;

public class FileSystemUrlProvider(ILogger<FileSystemUrlProvider> logger, IOptionsMonitor<FileSystemDownloaderSettings> options, IFileSystemDataProvider provider) : IUrlProvider
{
    public async Task<string[]> GetUrlsAsync()
    {
        var urlsWithNoQuerystring = (await provider.ReadAllLinesAsync(options.CurrentValue.UrlPaths))
            .Select(url => url.Split('?').First())
            .Distinct();
        var result = new List<string>();
        foreach (var url in urlsWithNoQuerystring)
        {
            if(Uri.TryCreate(url, UriKind.Absolute, out var validatedUrl)) result.Add(validatedUrl.ToString());
        }
        return result.ToArray();
    }
}