using System.Text;
using System.Text.Json;
using HttpDancer.Core;
using HttpDancer.Core.Http.Clients;
using HttpDancer.Core.Http.Downloading;
using HttpDancer.Extensions;
using HttpDancer.FileSystemDownloader.Data;
using HttpDancer.Html;
using HttpDancer.KnownMediaTypes;
using HttpDancer.Naming;
using HttpDancer.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinificationSettings = HttpDancer.FileSystemDownloader.Configuration.MinificationSettings;

namespace HttpDancer.FileSystemDownloader;

public interface IFileSystemDownloadRunner
{
    Task<bool> Run(string[] urls, bool crawlLinkedPages = false);
}

public class FileSystemDownloadRunner(
    ILoggerFactory loggerFactory, 
    ILogger<FileSystemDownloadRunner> logger,
    IKnowMediaTypes mediaTypes,
    ILinkParser linkParser,
    IUrlNamer urlNamer,
    IOptionsMonitor<MinificationSettings> minificationSettingsOptionsMonitor, 
    IOptionsMonitor<HttpDancerSettings> httpDancerSettingsOptionsMonitor, 
    IFileSystemDataProvider fileSystemDataProvider,
    IHtmlQuery htmlQuery,
    IHtmlStringsProvider htmlStringsParser,
    IHttpClient defaultApiClient) : IFileSystemDownloadRunner
{
    public async Task<bool> Run(string[] urls, bool crawlLinkedPages = false)
    {
        logger.LogInformation("Starting download job. UrlCount={Count}, CrawlLinkedPages={Crawl}", urls.Length, crawlLinkedPages);

        var httpDancerSeSettingsMonitorValue = httpDancerSettingsOptionsMonitor.CurrentValue;
        var channeledDownloadRequest = new ChanneledDownloadRequest()
        {
            Urls = urls,
            MaxConcurrentRequests = httpDancerSeSettingsMonitorValue.DefaultClient.MaxConcurrentRequests,
            MaxRequests = httpDancerSeSettingsMonitorValue.DefaultClient.MaxRequests, 
            RequestAsync = HandleRequestAsync,
            ShouldReadBodyAsync = HandleShouldReadBodyAsync,
            ResponseAsync = (channeledDownloadService, downloadResponse) => 
                HandleResponseAsync(channeledDownloadService, downloadResponse, crawlLinkedPages),
            StatusAsync = HandleStatusAsync
        };

        var loggerForChanneledDownloadService = loggerFactory.CreateLogger<ChanneledDownloadService>();

        var downloaderService = new ChanneledDownloadService(loggerForChanneledDownloadService, defaultApiClient, channeledDownloadRequest);
        
        var downloadResult = await downloaderService.DownloadAsync();



        logger.LogInformation("Download job completed. Success={Success}", downloadResult);

        return downloadResult;
    }
    

    private UrlAndPathInformation? GetUrlAndPathInformation(DownloadResponse downloadResponse, string[]? excludePaths = null)
    {
        if (string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
        {
            return null;
        }

        var urlNamingResult = urlNamer.GetNameAndPath(downloadResponse.Value.Url);
        var path = urlNamingResult.Path;
        var name = urlNamingResult.Name;
        var extension = mediaTypes.GetExtension(downloadResponse.Value.ContentType);

        return new UrlAndPathInformation {Url = downloadResponse.Value.Url, Path = path, Name = name, Extension = extension};        
    }

    private async Task HandleResponseAsync(ChanneledDownloadService downloaderService, DownloadResponse downloadResponse, bool crawlLinkedPages = false)
    {
        logger.LogDebug("Handling response for {Url} with ContentType {ContentType}", downloadResponse.Value.Url, downloadResponse.Value.ContentType);

        if (string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
        {
            logger.LogDebug("Skipping response with empty URL. Should never happen, but here we are o_=");
            return;
        }

        if (string.IsNullOrWhiteSpace(downloadResponse.Value.ContentType))
        {
            logger.LogDebug("Skipping {Url} due to missing ContentType.", downloadResponse.Value.Url);
            return;
        }

        if (downloadResponse.Value.ContentType.IsMatch(httpDancerSettingsOptionsMonitor.CurrentValue.DefaultClient.AllowedContentTypes) is not true)
        {
            logger.LogInformation("Skipping {Url} due to disallowed content type {ContentType}.", downloadResponse.Value.Url, downloadResponse.Value.ContentType);
            return;
        }
        
        // Crawl links when body has bytes and crawlLinkedPages is true

        if ( downloadResponse.Value.BodyBytes is { Length: > 0} && crawlLinkedPages)
        {
            var utf8EncodedString = Encoding.UTF8.GetString(downloadResponse.Value.BodyBytes);

            if (!string.IsNullOrWhiteSpace(utf8EncodedString) && !string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
            {
                var urls = linkParser.GetLinks(utf8EncodedString, downloadResponse.Value.Url)
                    .DistinctBy(link => link.Uri.ToString())
                    .Select(url => url.Uri.ToString())
                    .ToArray();

                downloaderService.EnqueueUrls(urls);

                logger.LogInformation("Enqueued {Count} discovered links from {Url}.", urls.Length, downloadResponse.Value.Url);
            }
            logger.LogDebug("No crawlable content for {Url}.", downloadResponse.Value.Url);
        }
        else
        {
            logger.LogDebug("No crawlable content for {Url}.", downloadResponse.Value.Url);
        }

        // Resolve path and name for the response
    
        var urlAndPathInformation = GetUrlAndPathInformation(downloadResponse, []);

        if (urlAndPathInformation is null)
        {
            logger.LogDebug("Unable to resolve UrlAndPathInformation for {Url}.", downloadResponse.Value.Url);
            return;
        }

        // Persist metadata

        var metaDataJsonString = JsonSerializer.Serialize(downloadResponse.Value, new JsonSerializerOptions { WriteIndented = true });
        
        await fileSystemDataProvider.WriteStringAsync($"{urlAndPathInformation.PathNameAndExtension}.json", metaDataJsonString);
        logger.LogDebug("Saved metadata JSON for {Url}.", downloadResponse.Value.Url);

        if (downloadResponse.Value.BodyBytes is not { Length: > 0 })
        {
            logger.LogDebug("Skipping further processing of {Url} because BodyBytes is empty.", downloadResponse.Value.Url);
            return;
        }

        // Persist content
        
        if (minificationSettingsOptionsMonitor.CurrentValue.Enabled)
        {
            logger.LogDebug("Minifying HTML for {Url}.", downloadResponse.Value.Url);
            downloadResponse = await MinifyHtml(downloadResponse);
        }
        
        await fileSystemDataProvider.WriteBytesAsync(urlAndPathInformation.PathNameAndExtension, downloadResponse.Value.BodyBytes!);
        logger.LogDebug("Saved content to {Path}", urlAndPathInformation.PathNameAndExtension);

        
        // Persist data
        
        var isTextual = downloadResponse.Value.ContentType!.IsMatch("text/*");
        
        if (downloadResponse.Value.BodyBytes is null || isTextual is not true)
        {
            logger.LogDebug("Not crawling links for {Url} (non-textual content).", downloadResponse.Value.Url);
            return;
        }

        var html = Encoding.UTF8.GetString(downloadResponse.Value.BodyBytes);
        var dataDictionary = await htmlStringsParser.GetAsync(html, CancellationToken.None);
        var dataJsonString = JsonSerializer.Serialize(dataDictionary, new JsonSerializerOptions { WriteIndented = true });
        
        await fileSystemDataProvider.WriteStringAsync($"{urlAndPathInformation.PathNameAndExtension}.data.json", dataJsonString);
        logger.LogDebug("Saved data JSON for {Url}.", downloadResponse.Value.Url);
    }

    private async Task<bool?> HandleShouldReadBodyAsync(CompletedHttpResponseMessage completedHttpResponseMessage)
    {
        var httpDancerSeSettingsMonitorValue = httpDancerSettingsOptionsMonitor.CurrentValue;
        var contentTypeShouldDownloadContent = completedHttpResponseMessage.ContentType?.IsMatch(httpDancerSeSettingsMonitorValue.DefaultClient.AllowedContentTypes) is true;

        logger.LogDebug("ShouldReadBody? Url={Url}, ContentType={ContentType}, Decision={Decision}", completedHttpResponseMessage.Url, completedHttpResponseMessage.ContentType, contentTypeShouldDownloadContent);
        return contentTypeShouldDownloadContent;
    }

    private async Task<RequestDecision> HandleRequestAsync(string requestUrl)
    {
        var httpDancerSeSettingsMonitorValue = httpDancerSettingsOptionsMonitor.CurrentValue;
        var requestShouldProceed = requestUrl.IsMatch(httpDancerSeSettingsMonitorValue.DefaultClient.AllowedHosts);
        logger.LogDebug("Request decision for {Url}: {Decision}", requestUrl, requestShouldProceed ? "Proceed" : "Skip");
        return requestShouldProceed
            ? RequestDecision.Proceed
            : RequestDecision.SkipPermanently;
    }

    private Task HandleStatusAsync(ChanneledDownloadService _, StatusResponse status)
    {
        var remainingBudget = Math.Max(0, status.MaxRequests - status.ScheduledCount);

        Console.Clear();
        
        logger.LogInformation(
            "Status: Processed={Processed}/{MaxRequests}, Pending={Pending}, InFlight={InFlight}, Scheduled={Scheduled}, RemainingBudget={RemainingBudget}, EstimatedRemaining={Remaining}",
            status.ProcessedCount,
            status.MaxRequests,
            status.PendingCount,
            status.InFlightCount,
            status.ScheduledCount,
            remainingBudget,
            status.RemainingCount);

        return Task.CompletedTask;
    }
    
    private async Task<DownloadResponse> MinifyHtml(DownloadResponse downloadResponse)
    {
        if(downloadResponse.Value.ContentType?.IsMatch("text/html") is not true || downloadResponse.Value.BodyBytes is null)
        {
            logger.LogDebug("Skipping minification for {Url} (ContentType={ContentType}, HasBody={HasBody})",
                downloadResponse.Value.Url,
                downloadResponse.Value.ContentType,
                downloadResponse.Value.BodyBytes is not null);
            return downloadResponse;
        }

        var utf8EncodedHtmlString = Encoding.UTF8.GetString(downloadResponse.Value.BodyBytes);

        if (string.IsNullOrWhiteSpace(utf8EncodedHtmlString))
        {
            logger.LogDebug("Empty HTML content for {Url}, skipping minification.", downloadResponse.Value.Url);
            return downloadResponse;
        }
        
        string minifiedUtf8EncodedHtmlString;
        try
        {
            var minifiedResult = await htmlQuery.MinifyQueryAsync(
                utf8EncodedHtmlString,
                ["*"],
                cancellationToken: CancellationToken.None);
            
            minifiedUtf8EncodedHtmlString = minifiedResult.FirstOrDefault()?.Value ?? utf8EncodedHtmlString;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Minification failed for {Url}; preserving original content.", downloadResponse.Value.Url);
            return downloadResponse;
        }
        
        if(string.IsNullOrWhiteSpace(minifiedUtf8EncodedHtmlString))
        {
            logger.LogDebug("Minification produced empty output for {Url}.", downloadResponse.Value.Url);
            return downloadResponse;
        }

        var utf8EncodedHtmlStringBytes = Encoding.UTF8.GetBytes(minifiedUtf8EncodedHtmlString);
        
        downloadResponse.Value.BodyBytes = utf8EncodedHtmlStringBytes;

        logger.LogDebug("Minified HTML for {Url} (OriginalLength={Original}, MinifiedLength={Minified})",
            downloadResponse.Value.Url,
            utf8EncodedHtmlString.Length,
            minifiedUtf8EncodedHtmlString.Length);
        
        return downloadResponse;
    }
}
