using HttpDancer.Core.Naming.Enums;

namespace HttpDancer.Core.Naming;

/// <summary>
/// Configuration object describing how to turn a URL into a logical path and name.
/// This POCO ties together all the strategy enums and basic scalar settings that
/// influence the URL-to-name/path conversion pipeline.
/// </summary>
public sealed class UrlNamingOptions
{

    public const string Key = "HttpDancer.UrlNamingOptions";
    
    /// <summary>
    /// Creates a new instance of <see cref="UrlNamingOptions"/> with sane default values
    /// intended for general web content (pages and media).
    /// </summary>
    public UrlNamingOptions()
    {
    }

    /// <summary>
    /// Gets a reusable default configuration instance representing the library's
    /// recommended baseline behavior for URL-to-path/name conversion.
    /// </summary>
    public static UrlNamingOptions Default => new UrlNamingOptions();

    /// <summary>
    /// Gets or sets the strategy that controls how trailing slashes are interpreted
    /// and normalized before deriving path and name.
    /// Default is <see cref="TrailingSlashStrategy.Ignore"/>, meaning
    /// "/documents/item" and "/documents/item/" are treated equivalently.
    /// </summary>
    public TrailingSlashStrategy TrailingSlash { get; set; } = TrailingSlashStrategy.Ignore;

    /// <summary>
    /// Gets or sets the high-level strategy that determines how the query string
    /// influences the generated name (ignored, appended or hashed).
    /// Default is <see cref="QueryStringStrategy.Ignore"/>, meaning query strings
    /// do not affect the resulting name.
    /// </summary>
    public QueryStringStrategy QueryStringStrategy { get; set; } = QueryStringStrategy.Ignore;

    /// <summary>
    /// Gets or sets the flags that describe which parts of the query string are
    /// considered when building an appended or hashed representation.
    /// Default is <see cref="QueryParts.None"/>, meaning nothing is included.
    /// This property is meaningful when <see cref="QueryStringStrategy"/> is
    /// <see cref="QueryStringStrategy.Append"/> or
    /// <see cref="QueryStringStrategy.Hash"/>.
    /// </summary>
    public QueryParts QueryParts { get; set; } = QueryParts.None;

    /// <summary>
    /// Gets or sets the strategy that controls how file extensions in the last
    /// path segment are treated when deriving the logical name.
    /// Default is <see cref="ExtensionStrategy.PageVsAsset"/>, which strips
    /// typical page extensions (e.g. ".html") while preserving document or asset
    /// extensions (e.g. ".pdf").
    /// </summary>
    public ExtensionStrategy ExtensionStrategy { get; set; } = ExtensionStrategy.PageVsAsset;

    /// <summary>
    /// Gets or sets the strategy defining how root-like URLs (e.g. "/") or top-level
    /// language URLs (e.g. "/da") receive a logical name.
    /// Default is <see cref="RootNamingStrategy.UseFixedName"/>, which uses the
    /// fixed name provided by <see cref="FixedRootName"/> for root URLs.
    /// </summary>
    public RootNamingStrategy RootNamingStrategy { get; set; } = RootNamingStrategy.UseFixedName;

    /// <summary>
    /// Gets or sets the strategy indicating how default documents such as "index.html"
    /// are interpreted when encountered at the end of the path.
    /// Default is <see cref="IndexDocumentStrategy.TreatAsFolder"/>, which treats
    /// default documents as representing their parent folder rather than a separate file.
    /// </summary>
    public IndexDocumentStrategy IndexDocumentStrategy { get; set; } = IndexDocumentStrategy.TreatAsFolder;

    /// <summary>
    /// Gets or sets the strategy determining how casing is normalized for path and
    /// name segments.
    /// Default is <see cref="CasingStrategy.LowercaseInvariant"/>, which converts
    /// segments to lowercase using invariant culture for stable behavior.
    /// </summary>
    public CasingStrategy CasingStrategy { get; set; } = CasingStrategy.LowercaseInvariant;

    /// <summary>
    /// Gets or sets the strategy describing how URL decoding and Unicode normalization
    /// are applied to path segments before further processing.
    /// Default is <see cref="DecodingStrategy.DecodeAndNormalize"/>, which fully
    /// decodes URL-encoded characters and normalizes Unicode.
    /// </summary>
    public DecodingStrategy DecodingStrategy { get; set; } = DecodingStrategy.DecodeAndNormalize;

    /// <summary>
    /// Gets or sets the slug strategy used to transform decoded text into a
    /// human-friendly, file-system- or URL-safe identifier.
    /// Default is <see cref="SlugStrategy.StrictSlug"/>, which enforces a typical
    /// slug format (e.g. "my-document" from "My Document!").
    /// </summary>
    public SlugStrategy SlugStrategy { get; set; } = SlugStrategy.StrictSlug;

    /// <summary>
    /// Gets or sets the strategy controlling if and how host and subdomain information
    /// are included when constructing the logical path.
    /// Default is <see cref="HostStrategy.IgnoreHost"/>, which ignores host and
    /// subdomain and uses only the URL path.
    /// </summary>
    public HostStrategy HostStrategy { get; set; } = HostStrategy.IgnoreHost;
    
    /// <summary>
    /// Gets or sets the flags indicating which normalization steps are applied when
    /// transforming raw path segments into their canonical form.
    /// Default is <see cref="NormalizationSteps.UrlDecode"/> |
    /// <see cref="NormalizationSteps.UnicodeNormalize"/> |
    /// <see cref="NormalizationSteps.Trim"/>, which decodes, normalizes and trims
    /// segments without forcing casing or "slugification" beyond what the dedicated
    /// strategies handle.
    /// </summary>
    public NormalizationSteps NormalizationSteps { get; set; } =
        NormalizationSteps.UrlDecode |
        NormalizationSteps.UnicodeNormalize |
        NormalizationSteps.Trim;

    /// <summary>
    /// Gets or sets the flags indicating which optional components (host
    /// or query hash) should be included as part of the logical path.
    /// Default is <see cref="PathComponents.None"/>, meaning only the raw path
    /// segments (after language handling) are used.
    /// </summary>
    public PathComponents PathComponents { get; set; } = PathComponents.None;

    /// <summary>
    /// Gets or sets the fixed name to use when <see cref="RootNamingStrategy"/> is
    /// <see cref="RootNamingStrategy.UseFixedName"/>.
    /// The default value is "index", meaning "/" will typically produce Name = "index".
    /// </summary>
    public string FixedRootName { get; set; } = "index";

    /// <summary>
    /// Gets or sets the array of file names (without a path) that should be treated as
    /// default index documents when <see cref="IndexDocumentStrategy"/> is
    /// <see cref="IndexDocumentStrategy.TreatAsFolder"/>.
    /// Default includes "index" and "default", with or without extensions.
    /// </summary>
    public string[] DefaultDocumentNames { get; set; } = ["index", "default"];

    /// <summary>
    /// Gets or sets the list of file extensions (including leading dot) that are
    /// considered "page" extensions for the purpose of
    /// <see cref="ExtensionStrategy.StripKnownPageExtensions"/> and
    /// <see cref="ExtensionStrategy.PageVsAsset"/>.
    /// Default includes ".html", ".htm" and ".aspx".
    /// </summary>
    public string[] PageExtensions { get; set; } = [".html", ".htm", ".aspx"];

    /// <summary>
    /// Gets or sets path segments or multi-segment patterns that should be removed
    /// from the URL path before any other processing.
    /// Entries are treated as simple substring replacements (case-insensitive),
    /// after which redundant slashes are collapsed.
    /// Example: ["media/-", "1990/jan", "years"] applied to "/media/-/years/1990/jan/people/item.jpg"
    /// becomes "///people/item.jpg" before normalization.
    /// </summary>
    public string[] ExcludedPathSegments { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the list of query string keys that should always be ignored when
    /// constructing appended or hashed representations of the query string.
    /// Typical examples are analytics parameters such as "utm_source" or "utm_campaign".
    /// Default is an empty array, meaning no keys are ignored by default.
    /// </summary>
    public string[] IgnoredQueryKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum allowed length for the final generated name.
    /// If a generated name exceeds this length, the implementation may truncate,
    /// hash or otherwise reduce it safely.
    /// Default is 128 characters.
    /// </summary>
    public int MaxNameLength { get; set; } = 128;

    /// <summary>
    /// Gets or sets the replacement character used when unsafe or disallowed characters
    /// need to be replaced in the generated name (for example, after slugification).
    /// Default is '-'.
    /// </summary>
    public char ReplacementChar { get; set; } = '-';

    /// <summary>
    /// Gets or sets a value indicating whether, in the event of a collision between
    /// two distinct URLs that generate the same name, the implementation is allowed
    /// to append a counter or disambiguator suffix.
    /// Default is true, allowing the implementation to produce unique names like
    /// "document", "document-2", "document-3", and so on.
    /// </summary>
    public bool AllowCollisionDisambiguation { get; set; } = true;
}
