using System;
using System.Text;
using System.Text.Json;
using HttpDancer.Core.Configuration;
using HttpDancer.Core.Http.Clients;
using HttpDancer.Core.Http.Downloading;
using HttpDancer.Extensions;
using HttpDancer.FileSystemDownloader.Minification;
using HttpDancer.Utilities;
using HttpDancer.Utilities.Parsing;
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
    IOptionsMonitor<MinificationSettings> minificationSettingsOptionsMonitor, 
    IOptionsMonitor<HttpDancerSettings> httpDancerSettingsOptionsMonitor, 
    IFileSystemDataProvider fileSystemDataProvider,
    IHttpClient defaultApiClient) : IFileSystemDownloadRunner
{
    public async Task<bool> Run(string[] urls, bool crawlLinkedPages = false)
    {
        logger.LogInformation("Starting download job. UrlCount={Count}, CrawlLinkedPages={Crawl}", urls.Length, crawlLinkedPages);

        var httpDancerSeSettingsMonitorValue = httpDancerSettingsOptionsMonitor.CurrentValue;
        var channeledDownloadRequest = new ChanneledDownloadRequest()
        {
            Urls = urls,
            MaxConcurrentRequests = httpDancerSeSettingsMonitorValue.MaxConcurrentRequests,
            MaxRequests = httpDancerSeSettingsMonitorValue.MaxRequests, 
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
    
    private static string[] ExcludedUrlParts = ["~", "-/media", "/images", "/english"];

    private static UrlInformation? GetUrlInformation(DownloadResponse downloadResponse)
    {
        if (string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
        {
            return null;
        }

        var path = UrlNaming.GetPath(downloadResponse.Value.Url, ExcludedUrlParts).Trim('/').Replace("/", "\\");
        var name = UrlNaming.GetName(downloadResponse.Value.Url, true);
        var extension = MimeTypes.GetExtension(downloadResponse.Value.ContentType);

        return new UrlInformation {Url = downloadResponse.Value.Url, Path = path, Name = name, Extension = extension};        
    }

    private async Task HandleResponseAsync(ChanneledDownloadService downloaderService, DownloadResponse downloadResponse, bool crawlLinkedPages = false)
    {
        logger.LogDebug("Handling response for {Url} with ContentType {ContentType}", downloadResponse.Value.Url, downloadResponse.Value.ContentType);

        if (string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
        {
            logger.LogDebug("Skipping response with empty URL.");
            return;
        }

        if (string.IsNullOrWhiteSpace(downloadResponse.Value.ContentType))
        {
            logger.LogDebug("Skipping {Url} due to missing ContentType.", downloadResponse.Value.Url);
            return;
        }

        if (downloadResponse.Value.ContentType.IsMatch(httpDancerSettingsOptionsMonitor.CurrentValue.AllowedContentTypes) is not true)
        {
            logger.LogInformation("Skipping {Url} due to disallowed content type {ContentType}.", downloadResponse.Value.Url, downloadResponse.Value.ContentType);
            return;
        }

        if (downloadResponse.Value.BodyBytes is not { Length: > 0 })
        {
            logger.LogInformation("Skipping {Url} because BodyBytes is empty.", downloadResponse.Value.Url);
            return;
        }
    
        var urlInformation = GetUrlInformation(downloadResponse);

        if (urlInformation is null)
        {
            logger.LogDebug("Unable to resolve UrlInformation for {Url}.", downloadResponse.Value.Url);
            return;
        }
        
        // fileSystemDataProvider.CreateDirectory(urlInformation.Path);
        
        if (minificationSettingsOptionsMonitor.CurrentValue.Enabled)
        {
            logger.LogInformation("Minifying HTML for {Url}.", downloadResponse.Value.Url);
            downloadResponse = await MinifyHtml(downloadResponse);
        }
        
        await fileSystemDataProvider.WriteBytesAsync(urlInformation.PathNameAndExtension, downloadResponse.Value.BodyBytes!);
        logger.LogInformation("Saved content to {Path}", urlInformation.PathNameAndExtension);

        var metaDataJsonString = JsonSerializer.Serialize(downloadResponse.Value, new JsonSerializerOptions { WriteIndented = true });

        await fileSystemDataProvider.WriteStringAsync($"{urlInformation.PathNameAndExtension}.json", metaDataJsonString);
        logger.LogDebug("Saved metadata JSON for {Url}.", downloadResponse.Value.Url);

        var isTextual = downloadResponse.Value.ContentType!.IsMatch("text/*");

        if (downloadResponse.Value.BodyBytes is null || isTextual is not true)
        {
            logger.LogDebug("Not crawling links for {Url} (non-textual content).", downloadResponse.Value.Url);
            return;
        }

        if (crawlLinkedPages)
        {
            var utf8EncodedString = Encoding.UTF8.GetString(downloadResponse.Value.BodyBytes);

            if (string.IsNullOrWhiteSpace(utf8EncodedString) || string.IsNullOrWhiteSpace(downloadResponse.Value.Url))
            {
                logger.LogDebug("No crawlable content for {Url}.", downloadResponse.Value.Url);
                return;
            }

            var urls = LinkParser.GetLinks(utf8EncodedString, downloadResponse.Value.Url)
                .DistinctBy(link => link.Uri.ToString())
                .Select(url => url.Uri.ToString().Split('?').First())
                .ToArray();

            downloaderService.EnqueueUrls(urls);
            logger.LogInformation("Enqueued {Count} discovered links from {Url}.", urls.Length, downloadResponse.Value.Url);
        }
    }

    private async Task<bool?> HandleShouldReadBodyAsync(ResponseMessage responseMessage)
    {
        var contentTypeShouldDownloadContent = true; // responseMessage.ContentType?.IsMatch("text/*") is true;

        logger.LogDebug("ShouldReadBody? Url={Url}, ContentType={ContentType}, Decision={Decision}", responseMessage.Url, responseMessage.ContentType, contentTypeShouldDownloadContent);
        return contentTypeShouldDownloadContent;
    }

    private async Task<RequestDecision> HandleRequestAsync(string requestUrl)
    {
        var httpDancerSeSettingsMonitorValue = httpDancerSettingsOptionsMonitor.CurrentValue;
        var requestShouldProceed = requestUrl.IsMatch(httpDancerSeSettingsMonitorValue.AllowedHosts);
        logger.LogDebug("Request decision for {Url}: {Decision}", requestUrl, requestShouldProceed ? "Proceed" : "Skip");
        return requestShouldProceed
            ? RequestDecision.Proceed
            : RequestDecision.SkipPermanently;
    }

    private Task HandleStatusAsync(ChanneledDownloadService _, StatusResponse status)
    {
        var remainingBudget = Math.Max(0, status.MaxRequests - status.ScheduledCount);

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
            minifiedUtf8EncodedHtmlString = AngleSharpHtmlMinifier.Minify(utf8EncodedHtmlString, minificationSettingsOptionsMonitor.CurrentValue.RemoveSelectors);
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

        logger.LogInformation("Minified HTML for {Url} (OriginalLength={Original}, MinifiedLength={Minified})",
            downloadResponse.Value.Url,
            utf8EncodedHtmlString.Length,
            minifiedUtf8EncodedHtmlString.Length);
        
        return downloadResponse;
    }
}
