using HttpDancer.Html;

namespace HttpDancer.FileSystemDownloader.Data;

public sealed class HtmlStringQuery
{
    public string Alias { get; set; } = string.Empty;
    public string[] Selectors { get; set; } = [];
    public string[] RemoveSelectors { get; set; } = [];

    public HtmlValueStrategy ValueStrategy { get; set; }

    public HtmlValueInstructions ValueInstructions { get; set; }
    
    public string AttributeName { get; set; } = "content";

}