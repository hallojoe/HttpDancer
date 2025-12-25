using System.Text.Json.Serialization;

namespace HttpDancer.Naming.Enums;

/// <summary>
/// Controls how trailing slashes are interpreted and normalized when deriving path and name from a URL.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrailingSlashStrategy
{
    /// <summary>
    /// Treat URLs with and without trailing slash as equivalent, preserving the original form
    /// without forcing a specific normalization.
    /// Example: "/documents/item" and "/documents/item/" produce the same path/name.
    /// </summary>
    Ignore,

    /// <summary>
    /// Treat URLs with and without trailing slash as distinct resources.
    /// Example: "/documents/item" and "/documents/item/" may produce different path/name pairs.
    /// </summary>
    Distinct,

    /// <summary>
    /// Normalize URLs to always have a trailing slash in their canonical form before deriving
    /// path and name.
    /// Example: "/documents/item" becomes "/documents/item/".
    /// </summary>
    NormalizeToSlash,

    /// <summary>
    /// Normalize URLs to never have a trailing slash in their canonical form (except root "/")
    /// before deriving path and name.
    /// Example: "/documents/item/" becomes "/documents/item".
    /// </summary>
    NormalizeWithoutSlash,

    /// <summary>
    /// Treat any URL ending with a trailing slash as representing a folder whose logical name
    /// should be "index" (or whatever is configured via RootNamingStrategy and FixedRootName).
    /// Example: "/documents/" => Path = "documents", Name = "index".
    /// Example: "/documents/item/" => Path = "documents/item", Name = "index".
    /// </summary>
    TreatTrailingSlashAsIndex
}

/// <summary>
/// Controls the overall handling strategy for query strings when generating a name from a URL.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryStringStrategy
{
    /// <summary>
    /// Ignore the query string entirely when computing the name.
    /// Example: "/documents/item?a=b" => "item".
    /// </summary>
    Ignore,

    /// <summary>
    /// Append a textual representation of selected parts of the query string to the base name.
    /// Example: "/documents/item?a=b&amp;c=d" => "item.a.b.c.d" (depending on QueryParts flags).
    /// </summary>
    Append,

    /// <summary>
    /// Generate a hash based on selected parts of the query string and append it to the base name.
    /// Example: "/documents/item?a=b&amp;c=d" => "item-hkfshd898".
    /// </summary>
    Hash
}

/// <summary>
/// Flag set defining which parts of the query string are considered when building a textual or hashed representation.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryParts
{
    /// <summary>
    /// Do not include any part of the query string.
    /// Useful with QueryStringStrategy.Ignore or as a default value.
    /// </summary>
    None = 0,

    /// <summary>
    /// Include query parameter names (keys).
    /// Example: "a=b&amp;c=d" contributes "a" and "c".
    /// </summary>
    Keys = 1,

    /// <summary>
    /// Include query parameter values.
    /// Example: "a=b&amp;c=d" contributes "b" and "d".
    /// </summary>
    Values = 2,

    /// <summary>
    /// Sort query parameters by key (and possibly value) before processing.
    /// Ensures stable output for differently ordered query strings.
    /// </summary>
    SortKeys = 4,

    /// <summary>
    /// Normalize all keys and values to lowercase before including them.
    /// Helps avoid casing-related duplicates.
    /// </summary>
    Lowercase = 8
}

/// <summary>
/// Controls how file extensions in the last path segment are treated when deriving the logical name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExtensionStrategy
{
    /// <summary>
    /// Keep all file extensions as part of the name.
    /// Example: "item.html" => "item.html", "file.pdf" => "file.pdf".
    /// </summary>
    KeepAll,

    /// <summary>
    /// Strip known page-oriented extensions such as .html, .htm, .aspx, etc., while keeping others.
    /// Example: "item.html" => "item", "file.pdf" => "file.pdf".
    /// </summary>
    StripKnownPageExtensions,

    /// <summary>
    /// Strip any extension from the last segment regardless of type.
    /// Example: "item.html" => "item", "file.pdf" => "file".
    /// </summary>
    StripAll,

    /// <summary>
    /// Treat page extensions (html-like) as removable and keep binary or document extensions.
    /// Example: "item.html" => "item", "file.pdf" => "file.pdf".
    /// </summary>
    PageVsAsset
}

/// <summary>
/// Defines how the root of a site or a top-level language segment should be named when no further path segments exist.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RootNamingStrategy
{
    /// <summary>
    /// Use an empty string as the name for root-like URLs.
    /// Example: "/" => Name = "".
    /// </summary>
    EmptyName,

    /// <summary>
    /// Use a fixed logical name such as "index" or "home" for root-like URLs.
    /// Example: "/" => Name = "index".
    /// </summary>
    UseFixedName,

    /// <summary>
    /// Use the last available segment as the name, even if it is a language code or top-level folder.
    /// Example: "/da" => Name = "da".
    /// </summary>
    UseLastSegment
}

/// <summary>
/// Defines how default documents like "index.html" are interpreted when deriving path and name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IndexDocumentStrategy
{
    /// <summary>
    /// Treat default documents as representing the parent folder rather than a separate file.
    /// Example: "/foo/index.html" => Path = "foo", Name = "foo" or an empty name, depending on RootNamingStrategy.
    /// </summary>
    TreatAsFolder,

    /// <summary>
    /// Treat default documents as real files with their own name.
    /// Example: "/foo/index.html" => Path = "foo", Name = "index".
    /// </summary>
    TreatAsFile
}

/// <summary>
/// Controls how casing of path and name segments is normalized.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CasingStrategy
{
    /// <summary>
    /// Preserve the original casing from the URL when generating path and name.
    /// </summary>
    Preserve,

    /// <summary>
    /// Convert path and name to lowercase using the current culture rules.
    /// </summary>
    Lowercase,

    /// <summary>
    /// Convert path and name to lowercase using invariant culture rules for stable, culture-agnostic behavior.
    /// </summary>
    LowercaseInvariant
}

/// <summary>
/// Controls how URL-encoding and Unicode normalization are applied to path segments and names.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DecodingStrategy
{
    /// <summary>
    /// Fully decode URL-encoded characters and apply Unicode normalization for consistent comparisons.
    /// Example: "%C3%B8" => "ø".
    /// </summary>
    DecodeAndNormalize,

    /// <summary>
    /// Decode only ASCII characters while leaving non-ASCII sequences as-is.
    /// Useful when you want to avoid aggressive Unicode transformations.
    /// </summary>
    DecodeOnlyAscii,

    /// <summary>
    /// Do not decode URL-encoded characters; keep segments in their encoded form.
    /// Example: "%20" remains "%20".
    /// </summary>
    KeepEncoded
}

/// <summary>
/// Controls how human-readable slugs are generated from decoded text for use in names or paths.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlugStrategy
{
    /// <summary>
    /// Do not modify the decoded text specifically for slugging; keep punctuation and whitespace as-is.
    /// </summary>
    None,

    /// <summary>
    /// Replace whitespace with dashes and perform minimal cleanup, without enforcing a strict slug character set.
    /// Example: "My Document Name" => "My-Document-Name".
    /// </summary>
    ReplaceSpacesWithDash,

    /// <summary>
    /// Enforce a strict slug format, typically allowing only [a-z0-9-] and replacing or stripping all other characters.
    /// Example: "Mě Døcument!" => "my-document".
    /// </summary>
    StrictSlug
}

/// <summary>
/// Controls how host and subdomain information is included or ignored when constructing the logical path.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HostStrategy
{
    /// <summary>
    /// Ignore host and subdomain entirely; only the URL path is used to derive the logical path.
    /// Example: "www.example.com/foo" => "foo".
    /// </summary>
    IgnoreHost,
    
    /// <summary>
    /// Include the full host name as a leading component in the path.
    /// Example: "www.example.com/foo" => "www.example.com/foo".
    /// </summary>
    IncludeHost
}

/// <summary>
/// Flag set describing which normalization steps are applied when transforming raw segments into canonical path and name.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NormalizationSteps
{
    /// <summary>
    /// Do not apply any normalization steps.
    /// Useful as a baseline or for debugging.
    /// </summary>
    None = 0,

    /// <summary>
    /// Perform URL decoding of percent-encoded characters before further processing.
    /// </summary>
    UrlDecode = 1 << 0,

    /// <summary>
    /// Apply Unicode normalization (e.g. Form C) after decoding to ensure consistent representation of characters.
    /// </summary>
    UnicodeNormalize = 1 << 1,

    /// <summary>
    /// Convert text to lowercase as part of normalization.
    /// Typically used together with ToLowerInvariant or culture-aware lowercasing.
    /// </summary>
    ToLower = 1 << 2,

    /// <summary>
    /// Strip diacritics from characters, mapping accented letters to their base forms.
    /// Example: "ø" => "o".
    /// </summary>
    StripDiacritics = 1 << 3,

    /// <summary>
    /// Generate or enforce a slug format for the segment (commonly [a-z0-9-]) as a normalization step.
    /// </summary>
    Slugify = 1 << 4,

    /// <summary>
    /// Trim leading and trailing whitespace and separator characters from the segment.
    /// </summary>
    Trim = 1 << 5
}

/// <summary>
/// Flag set indicating which optional components should be included in the resulting logical path.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PathComponents
{
    /// <summary>
    /// Do not include any optional components beyond the raw path segments.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Include the full host name (e.g. "www.example.com") as a leading path component.
    /// </summary>
    Host = 1 << 1,

    /// <summary>
    /// Include the file extension of the last segment as a separate component in the logical path, if desired.
    /// </summary>
    Extension = 1 << 3,

    /// <summary>
    /// Include a query-string-based hash as a dedicated component in the logical path, useful for versioned variants.
    /// </summary>
    QueryHash = 1 << 4
}
