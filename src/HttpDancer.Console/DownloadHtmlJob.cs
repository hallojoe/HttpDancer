using System.Text;
using System.Text.Json;
using HttpDancer.Core;
using HttpDancer.Core.Configuration;
using HttpDancer.Core.Http;
using HttpDancer.Utilities;
using HttpDancer.Utilities.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Console;

public class UrlInformation
{
    public required string Url { get; set; }
    public required string Path { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    
    public string NameAndExtension => $"{Name}.{Extension}";

    public string PathNameAndExtension => $"{System.IO.Path.Join(Path, Name)}.{Extension}";
}

public class DownloadHtmlJob(
    ILoggerFactory loggerFactory, 
    IOptions<ApiClientSettings> options, 
    IHttpClient defaultApiClient)
{
    //private ILogger<DownloadHtmlJob> logger = loggerFactory.CreateLogger<DownloadHtmlJob>();

    private static string[] ExcludedUrlParts = ["~", "-/media", "/images", "/english"];

    private static UrlInformation? GetUrlInformation(DownloadResponse downloadResponse)
    {
        if (string.IsNullOrWhiteSpace(downloadResponse.Value.Url)) return null;
        var path = UrlNaming.GetPath(downloadResponse.Value.Url, ExcludedUrlParts);
        var name = UrlNaming.GetName(downloadResponse.Value.Url, true);
        var extension = MimeTypes.GetExtension(downloadResponse.Value.ContentType);
        return new UrlInformation {Url = downloadResponse.Value.Url, Path = path, Name = name, Extension = extension};        
    }

    private static async Task SaveFileAsync(DownloadResponse downloadResponse, string workspacePath, string fileName)
    {
        if (downloadResponse.Value.BodyBytes is null) return;

        await File.WriteAllBytesAsync(Path.Join(workspacePath, fileName), downloadResponse.Value.BodyBytes);
    }

    public async Task<bool> DownloadHtml(string[] urls, bool crawlLinkedPages = false)
    {
        var downloadContext = new DownloadContext()
        {
            Urls = urls,
            MaxConcurrentRequests = 3,
            MaxRequests = 30, 
            ResponseAsync = async (downloaderService, downloadResponse) =>
            {
                if(string.IsNullOrWhiteSpace(downloadResponse.Value.Url)) return;
                
                if(string.IsNullOrWhiteSpace(downloadResponse.Value.ContentType)) return;

                if(downloadResponse.Value.BodyBytes is not {Length:>0}) return;

                var urlInformation = GetUrlInformation(downloadResponse);

                if(urlInformation is null) return;

                Directory.CreateDirectory(Path.Join(options.Value.Io.Workspace, urlInformation.Path));
                
                var fullFilePathAndName = Path.Join(options.Value.Io.Workspace, urlInformation.PathNameAndExtension);

                await File.WriteAllBytesAsync(fullFilePathAndName, downloadResponse.Value.BodyBytes);
                
                var metaDataJsonString = JsonSerializer.Serialize(downloadResponse.Value, new JsonSerializerOptions {WriteIndented = true});

                await File.WriteAllTextAsync($"{fullFilePathAndName}.json", metaDataJsonString);

                var isTextual = downloadResponse.Value.ContentType.Contains("text/", StringComparison.OrdinalIgnoreCase);

                if(downloadResponse.Value.BodyBytes is null || isTextual is not true) return;
                
                if (crawlLinkedPages)
                {
                    var utf8EncodedString = Encoding.UTF8.GetString(downloadResponse.Value.BodyBytes);

                    if (string.IsNullOrWhiteSpace(utf8EncodedString) || string.IsNullOrWhiteSpace(downloadResponse.Value.Url)) return;                

                    var links = LinkParser.GetLinks(utf8EncodedString, downloadResponse.Value.Url.ToString()).Distinct();
                
                    downloaderService.EnqueueUrls(links.Select(link => link.Uri.ToString()));
                }
            }
        };

        var loggerForChanneledDownloadService = loggerFactory.CreateLogger<ChanneledDownloadService>();

        var downloaderService = new ChanneledDownloadService(loggerForChanneledDownloadService, defaultApiClient, downloadContext);
        
        var downloadResult = await downloaderService.DownloadAsync();

        return downloadResult;
    }
}