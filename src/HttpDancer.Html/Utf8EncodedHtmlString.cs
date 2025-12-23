namespace HttpDancer.Html;

[Serializable]
public sealed class Utf8EncodedHtmlString : IUtf8EncodedHtmlString
{
    public string? Selector { get; set; }
    public string? TagName { get; set; }
    public string Value { get; set; } = string.Empty;
    public KeyValuePair<string, string?>[] Attributes { get; set; } = [];

    public override string ToString()
    {
        return Value;
    }
}