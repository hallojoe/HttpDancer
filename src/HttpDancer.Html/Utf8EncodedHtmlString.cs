namespace HttpDancer.Html;

public class Utf8EncodedHtmlString : IUtf8EncodedHtmlString
{
    public string? Selector { get; set; }
    public string? TagName { get; set; }
    public string Value { get; set; } = string.Empty;
    public KeyValuePair<string, string?>[] Attributes { get; set; } = [];
}