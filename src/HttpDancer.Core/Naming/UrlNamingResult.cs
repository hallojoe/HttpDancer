using HttpDancer.Core.Naming.Enums;

namespace HttpDancer.Core.Naming;

/// <summary>
/// Represents the canonical naming output produced from a URL,
/// including the logical path, name and selected metadata derived
/// during processing.
/// </summary>
public sealed class UrlNamingResult
{
    /// <summary>
    /// Gets or sets the canonical logical path derived from the URL.
    /// The path typically consists of normalized segments joined by '/'
    /// and may or may not include language or host components depending
    /// on the configured options.
    ///
    /// Example: "da/Organdonation/Godt-at-vide-om-organdonation".
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the canonical logical name derived from the URL.
    /// This is usually the final segment after all normalization,
    /// extension handling, media suffix logic and optional query
    /// influence have been applied.
    ///
    /// Example: "donation-efter-cirkulatorisk-doed" or
    /// "document-hkfshd898" depending on the scenario.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full canonical path including the name, if the
    /// implementation chooses to expose such a composite value.
    /// This is typically the <see cref="Path"/> and <see cref="Name"/>
    /// joined by a '/' separator.
    ///
    /// Example: "da/Organdonation/Godt-at-vide-om-organdonation/donation-efter-cirkulatorisk-doed".
    /// May be left empty if the implementation does not use it.
    /// </summary>
    public string? FullPath { get; set; }

    /// <summary>
    /// Gets or sets the normalized, canonical URL representation that was
    /// actually used as the basis for deriving <see cref="Path"/> and
    /// <see cref="Name"/>. This may differ from the input by having
    /// normalized casing, decoded characters, removed tracking parameters
    /// or normalized trailing slashes.
    /// </summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>
    /// Gets or sets the hash value that was derived from the query string,
    /// if <see cref="UrlNamingOptions.QueryStringStrategy"/> is configured
    /// to use hashing and the implementation chooses to surface the hash.
    /// This is typically appended to the name or included in
    /// <see cref="PathComponents.QueryHash"/> depending on configuration.
    /// </summary>
    public string? QueryHash { get; set; }

    /// <summary>
    /// Gets or sets an optional string containing diagnostic information
    /// or a human-readable description of the decisions taken when
    /// deriving the path and name. This can be useful for debugging or
    /// inspection in tooling, but is not required for normal operation.
    /// </summary>
    public string? Diagnostics { get; set; }
}