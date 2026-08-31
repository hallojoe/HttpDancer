using System.Collections.Concurrent;
using System.Text;
using HttpDancer.Core;
using HttpDancer.Core.Http.Clients;
using HttpDancer.Extensions;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.KnownMediaTypes;
using HttpDancer.Naming;
using HttpDancer.Scheduling.RatedScheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Downloading;

public sealed class DownloadRunService(
    IHttpClient httpClient,
    IHttpFileParser httpFileParser,
    IHttpFileRenderer httpFileRenderer,
    IRatedScheduleFactory ratedScheduleFactory,
    IRatedScheduleRunner ratedScheduleRunner,
    IUrlNamer urlNamer,
    IKnowMediaTypes mediaTypes,
    IOptionsMonitor<HttpDancerSettings> httpDancerSettings,
    ILogger<DownloadRunService> logger) : IDownloadRunService
{
    public async Task<DownloadRunResult> RunAsync(DownloadRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request.Options);

        var archive = FileSystemRunArchive.Create(request.Options.Workspace, request.SeedUri, urlNamer, mediaTypes);
        var seedResponse = await httpClient.SendAsync(new SerializableRequestMessage
        {
            Uri = request.SeedUri,
            Method = HttpMethod.Get,
            ReadBodyOnSuccess = true,
            ReadBodyOnNonSuccess = true
        }, cancellationToken).ConfigureAwait(false);

        if (!seedResponse.IsSuccessStatusCode || seedResponse.BodyBytes is not { Length: > 0 } || !IsTextual(seedResponse.ContentType))
        {
            await archive.WriteSeedFailureAsync(seedResponse, cancellationToken).ConfigureAwait(false);
            var message = seedResponse.Message ?? "Seed response was not successful textual content.";
            return new DownloadRunResult(request.SeedUri, archive.RunDirectory, 0, 0,
                [new DownloadFailure(-1, request.SeedUri, message)], false);
        }

        var sourceManifest = httpFileParser.Parse(
            Encoding.UTF8.GetString(seedResponse.BodyBytes),
            $"{request.SeedUri.Scheme}://{request.SeedUri.Authority}",
            HttpMethod.Get.Method,
            new HttpFileParseOptions(true, true));

        var requests = sourceManifest.Requests
            .GroupBy(item => item.Url.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(item => IsAllowedForDownload(item.Url, httpDancerSettings.CurrentValue.DefaultClient.DisallowedUrlPattern))
            .Take(request.Options.MaxRequestsPerSeed)
            .ToList();
        var manifest = sourceManifest with { Requests = requests };
        await archive.WriteManifestAsync("source.http", manifest, httpFileRenderer, cancellationToken).ConfigureAwait(false);

        var results = new ConcurrentDictionary<int, ScheduledDownloadResult>();
        var schedule = ratedScheduleFactory.Create(new RatedScheduleOptions
        {
            Capacity = requests.Count,
            Rate = request.Options.Rate,
            RateWindow = request.Options.RateWindow
        });
        var startedAt = DateTime.UtcNow;

        await ratedScheduleRunner.RunAsync(
            schedule.Offsets,
            async (index, offset, token) =>
            {
                var definition = requests[index];
                var plannedAt = startedAt.Add(offset);
                try
                {
                    var response = await httpClient.SendAsync(ToRequest(definition), token).ConfigureAwait(false);
                    await archive.WriteResponseAsync(response, token).ConfigureAwait(false);
                    results[index] = new ScheduledDownloadResult(index, definition, offset, plannedAt, DateTime.UtcNow, response, null);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Download failed for {Url}", definition.Url);
                    results[index] = new ScheduledDownloadResult(index, definition, offset, plannedAt, DateTime.UtcNow, null, exception.Message);
                }
            },
            maxDegreeOfParallelism: request.Options.MaxDegreeOfParallelism,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var resultManifest = BuildResultManifest(manifest, schedule, startedAt, results);
        await archive.WriteManifestAsync("results.http", resultManifest, httpFileRenderer, cancellationToken).ConfigureAwait(false);

        var failures = results.Values
            .Where(item => item.Error is not null || item.Response?.IsSuccessStatusCode is not true)
            .OrderBy(item => item.Index)
            .Select(item => new DownloadFailure(item.Index, item.Request.Url,
                item.Error ?? item.Response?.Message ?? $"HTTP {(int)item.Response!.StatusCode}"))
            .ToArray();
        var downloadedCount = results.Values.Count(item => item.Response?.IsSuccessStatusCode is true);
        return new DownloadRunResult(request.SeedUri, archive.RunDirectory, requests.Count, downloadedCount, failures, true);
    }

    private static SerializableRequestMessage ToRequest(HttpRequestDefinition definition) => new()
    {
        Uri = definition.Url,
        Method = definition.Method,
        Headers = definition.Headers
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (string?)string.Join(", ", group.Select(header => header.Value)), StringComparer.OrdinalIgnoreCase),
        Content = definition.Body is null ? null : new StringContent(definition.Body, Encoding.UTF8)
    };

    private static HttpFileDocument BuildResultManifest(
        HttpFileDocument source,
        RatedSchedule schedule,
        DateTime startedAt,
        IReadOnlyDictionary<int, ScheduledDownloadResult> results)
    {
        var requests = source.Requests.Select((definition, index) =>
        {
            results.TryGetValue(index, out var result);
            var headers = new List<HttpHeader>(definition.Headers)
            {
                new("X-Request-Offset", schedule.Offsets[index].ToString()),
                new("X-Response-Planned", startedAt.Add(schedule.Offsets[index]).ToString("O")),
                new("X-Response-Executed", result?.ExecutedDateTime?.ToString("O") ?? string.Empty),
                new("X-Response-Status", result?.Response is null ? "0" : ((int)result.Response.StatusCode).ToString())
            };
            if (!string.IsNullOrWhiteSpace(result?.Response?.CorrelationId)) headers.Add(new("X-Correlation-Id", result.Response.CorrelationId));
            if (!string.IsNullOrWhiteSpace(result?.Response?.Message)) headers.Add(new("X-Response-Message", result.Response.Message));
            if (!string.IsNullOrWhiteSpace(result?.Error)) headers.Add(new("X-Response-Error", result.Error));
            if (result?.Response?.Headers?.TryGetValue("Location", out var locations) is true && !string.IsNullOrWhiteSpace(locations.FirstOrDefault()))
                headers.Add(new("X-Response-Location", locations.First()));
            return definition with { Headers = headers };
        }).ToList();
        return source with { Requests = requests };
    }

    private static bool IsTextual(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
         contentType.StartsWith("application/", StringComparison.OrdinalIgnoreCase));

    private static bool IsAllowedForDownload(Uri uri, string? disallowedUrlPattern)
    {
        if (string.IsNullOrWhiteSpace(disallowedUrlPattern)) return true;

        // Match path patterns (for example "*.js") against the path, not the host.
        // The generic wildcard helper deliberately treats dot-containing patterns as host
        // patterns when its input is an absolute URI, which is useful elsewhere but wrong here.
        var isDisallowed = disallowedUrlPattern
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(pattern => (pattern.Contains("://", StringComparison.Ordinal)
                    ? uri.AbsoluteUri
                    : uri.PathAndQuery)
                .IsMatch(pattern));
        return !isDisallowed;
    }

    private static void Validate(DownloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRequestsPerSeed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Rate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDegreeOfParallelism);
        if (options.RateWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.RateWindow));
    }
}
