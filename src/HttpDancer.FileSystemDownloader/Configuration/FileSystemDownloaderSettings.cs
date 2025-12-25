namespace HttpDancer.FileSystemDownloader.Configuration;

public class FileSystemDownloaderSettings
{
    public const string Key = "HttpDancer.FileSystemDownloader";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string[] Urls { get; set; } = [];
    public string? Workspace { get; set; }
    /// <summary>
    /// Paths to textual contents files that contain URL's. Urls will be parsed from these files  
    /// </summary>
    public required string[] UrlPaths { get; set; }
    public required string? KnownHrefListPath { get; set; }
    public required string? SeenUrls { get; set; }
}