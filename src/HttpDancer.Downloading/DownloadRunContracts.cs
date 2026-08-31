using HttpDancer.FileFormats.HttpFile;

namespace HttpDancer.Downloading;

public sealed class DownloadOptions
{
    public const string Key = "HttpDancer:Download";

    public string Workspace { get; set; } = "archives";
    public string[] SeedUrls { get; set; } = [];
    public int MaxRequestsPerSeed { get; set; } = 100;
    public int Rate { get; set; } = 1;
    public TimeSpan RateWindow { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxDegreeOfParallelism { get; set; } = 8;
}

public sealed record DownloadRunRequest(Uri SeedUri, DownloadOptions Options);

public sealed record DownloadFailure(int Index, Uri Uri, string Message);

public sealed record DownloadRunResult(
    Uri SeedUri,
    string RunDirectory,
    int ManifestRequestCount,
    int DownloadedCount,
    IReadOnlyList<DownloadFailure> Failures,
    bool SeedSucceeded)
{
    public int FailedCount => Failures.Count;
}

public interface IDownloadRunService
{
    Task<DownloadRunResult> RunAsync(DownloadRunRequest request, CancellationToken cancellationToken = default);
}

internal sealed record ScheduledDownloadResult(
    int Index,
    HttpRequestDefinition Request,
    TimeSpan Offset,
    DateTime PlannedDateTime,
    DateTime? ExecutedDateTime,
    HttpDancer.Core.Http.Clients.CompletedHttpResponseMessage? Response,
    string? Error);
