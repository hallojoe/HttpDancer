namespace HttpDancer.FileSystemDownloader.Configuration;

public class MinificationSettings
{
    public const string Key = "Minification";
    public bool Enabled { get; set; }
    public List<string>? RemoveSelectors { get; set; }
}