namespace HttpDancer.FileSystemDownloader.Data;

/// <summary>
/// Setting for HtmlPage processing.
/// Instructions: Expect this to be available via DI.
/// </summary>
public class HtmlParsingSettings
{
    public const string Key = "HttpDancer.HtmlQuerying";
    public bool Enabled { get; set; }
    public HtmlStringQuery[] Queries { get; set; } = [];
}