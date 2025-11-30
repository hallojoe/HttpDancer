namespace HttpDancer.FileSystemDownloader;

public class UrlInformation
{
    public required string Url { get; set; }
    public required string Path { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    
    public string NameAndExtension => $"{Name}.{Extension}";

    public string PathNameAndExtension => $"{System.IO.Path.Join(Path, Name)}.{Extension}";
}