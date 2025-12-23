namespace HttpDancer.Html;

public interface IUtf8EncodedHtmlString
{
    /// <summary>
    /// Selector that was used to get the element.
    /// </summary>
    string? Selector { get; set; }
    /// <summary>
    /// The tag name of the element.
    /// </summary>
    string? TagName { get; set; }
    /// <summary>
    /// The outer HTML of the element.
    /// </summary>
    string Value { get; set; }
    /// <summary>
    /// The attributes and values of the outer HTML element.
    /// </summary>
    KeyValuePair<string, string?>[] Attributes { get; set; }
}
