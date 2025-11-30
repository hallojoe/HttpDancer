namespace HttpDancer.FileSystemDownloader;

public class FileSystemDataSettings
{
    public const string Key = "HttpDancer.FileSystemData";
    public bool Enabled { get; set; }
    public string? Workspace { get; set; }
    public required string[] HrefListPaths { get; set; }
    public required string? KnownHrefListPath { get; set; }
    public required string? SeenUrls { get; set; }
}