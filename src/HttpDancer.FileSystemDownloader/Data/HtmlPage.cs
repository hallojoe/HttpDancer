namespace HttpDancer.FileSystemDownloader.Data;

/// <summary>
/// Represents an HTML page and its parsed semantic metadata.
/// </summary>
public class HtmlPage
{
    /// <summary>
    /// Optional document type (news, page, post, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The documents source url.
    /// </summary>
    public required string SourceUrl { get; set; }

    /// <summary>
    /// The document title (from &lt;title&gt; or meta tags).
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The page description (typically meta description).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Arbitrary tags representing the page (keywords, categories, etc.).
    /// </summary>
    public string[] Tags { get; set; } = [];
    
    /// <summary>
    /// Date extracted from the page body or structured content.
    /// </summary>
    public DateTime? Date { get; set; } = null;

    /// <summary>
    /// Content date extracted from the page content body or structured content.
    /// </summary>
    public DateTime? ContentDate { get; set; } = null;

    /// <summary>
    /// Content title extracted from the page body or structured content.
    /// </summary>
    public string? ContentTitle { get; set; }

    /// <summary>
    /// A short lead or intro paragraph.
    /// </summary>
    public string? ContentLead { get; set; }

    /// <summary>
    /// The main body text/content extracted from the HTML.
    /// </summary>
    public string? ContentBody { get; set; }

    /// <summary>
    /// Any additional extracted content that does not fit Title/Lead/Body semantics.
    /// </summary>
    public Dictionary<string, string?> ContentAdditional { get; set; } = [];
    
    /// <summary>
    /// All meta tags parsed from the HTML page.
    /// </summary>
    public IReadOnlyList<HtmlMetaTag> Meta { get; set; } = [];

    /// <summary>
    /// All links to internal resources.
    /// </summary>
    public List<string> InternalLinks { get; set; } = [];
    
    /// <summary>
    /// All links to external resources.
    /// </summary>
    public List<string> ExternalLinks { get; set; } = [];
}

