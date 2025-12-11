namespace HttpDancer.FileSystemDownloader.Data;

/// <summary>
/// POCO representing a single HTML meta-tag.
/// </summary>
public class HtmlMetaTag
{
    /// <summary>
    /// The "name" attribute (e.g. name="description").
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The "content" attribute.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// The "http-equiv" attribute (e.g. http-equiv="refresh").
    /// </summary>
    public string? HttpEquiv { get; set; }

    /// <summary>
    /// The "charset" attribute (e.g. charset="utf-8").
    /// </summary>
    public string? Charset { get; set; }

    /// <summary>
    /// The "property" attribute (e.g. og:description, twitter:*).
    /// </summary>
    public string? Property { get; set; }

    /// <summary>
    /// The "itemprop" attribute (microdata).
    /// </summary>
    public string? ItemProp { get; set; }

    /// <summary>
    /// The raw HTML of the meta tag (optional, but handy for debugging).
    /// </summary>
    public string? RawHtml { get; set; }

    /// <summary>
    /// "head" or "body", indicating where the meta tag was found.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Any additional attributes on the tag that are not explicitly mapped.
    /// </summary>
    public IDictionary<string, string> AdditionalAttributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
