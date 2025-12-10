namespace HttpDancer.FileSystemDownloader.Configuration;

public class MinificationSettings
{
    public const string Key = "Minification";
    public bool Enabled { get; set; } = true;
    public string[]? RemoveSelectors { get; set; }
}
