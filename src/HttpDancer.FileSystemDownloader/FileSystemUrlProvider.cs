using HttpDancer.Core;
using HttpDancer.Extensions;
using HttpDancer.FileSystemDownloader.Configuration;
using HttpDancer.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.FileSystemDownloader;

public class FileSystemUrlProvider(
    ILogger<FileSystemUrlProvider> logger, 
    IOptionsMonitor<HttpDancerSettings> httpDancerOptions, 
    IOptionsMonitor<FileSystemDownloaderSettings> options, 
    ILinkParser linkParser,
    IFileSystemDataProvider provider) : IUrlProvider
{
    public async Task<string[]> GetUrlsAsync()
    {
        try
        {
            if (options.CurrentValue.UrlPaths is { Length:0 }) return options.CurrentValue.Urls;

            var urlCollection = new List<string>();

            var utf8EncodedStringCollection = await provider.ReadStringCollectionAsync(options.CurrentValue.UrlPaths);
            foreach (var utf8EncodedString in utf8EncodedStringCollection)
            {
                var links = linkParser.GetLinks(utf8EncodedString, options.CurrentValue.BaseUrl);   
                
                if (links is { Length:0 }) continue;

                foreach (var link in links)
                {
                    var url = link.Uri.ToString();

                    if (urlCollection.Contains(url))
                    {
                        continue;
                    }
                    
                    if (link.Uri.Host.IsMatch(httpDancerOptions.CurrentValue.DefaultClient.AllowedHosts))
                    {
                        urlCollection.Add(url);
                    }
                }
            }
            
            // var urlsWithNoQuerystring = (await provider.ReadAllLinesAsync(options.CurrentValue.UrlPaths))
            //     .Distinct();
            //
            // var result = new List<string>();
            // foreach (var url in urlsWithNoQuerystring)
            // {
            //     if(Uri.TryCreate(url, UriKind.Absolute, out var validatedUrl)) result.Add(validatedUrl.ToString());
            // }
            
            return urlCollection.ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load URLs from file system");
            throw;
        }
    }
}