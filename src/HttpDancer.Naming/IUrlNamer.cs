namespace HttpDancer.Naming;

/// <summary>
/// Defines the contract for a component that converts URLs into
/// canonical path and name pairs using a configured set of
/// <see cref="UrlNamingOptions"/>.
/// </summary>
public interface IUrlNamer
{
    /// <summary>
    /// Gets the options used to control how URLs are converted into
    /// logical path and name values.
    /// </summary>
    UrlNamingOptions Options { get; }

    /// <summary>
    /// Computes a canonical logical path and name for the given URL string
    /// according to the configured <see cref="Options"/>.
    /// Throws an exception if the URL is not a valid absolute or resolvable
    /// relative URL, depending on the implementation.
    ///
    /// Typical usage:
    /// - Input: "https://www.example.com/da/Organdonation/Artikel.html?a=b"
    /// - Output: Path = "da/Organdonation", Name = "artikel"
    ///   (exact outcome depends on options).
    /// </summary>
    /// <param name="url">The URL string to analyze.</param>
    /// <returns>
    /// A <see cref="UrlNamingResult"/> containing the derived path, name
    /// and related metadata.
    /// </returns>
    UrlNamingResult GetNameAndPath(string url);

    /// <summary>
    /// Computes a canonical logical path and name for the given <see cref="Uri"/>
    /// according to the configured <see cref="Options"/>.
    /// Throws an exception if the URI is not accepted by the implementation
    /// (for example, if it is not absolute and no base URI is available).
    /// </summary>
    /// <param name="uri">The URI to analyze.</param>
    /// <returns>
    /// A <see cref="UrlNamingResult"/> containing the derived path, name
    /// and related metadata.
    /// </returns>
    UrlNamingResult GetNameAndPath(Uri uri);

    /// <summary>
    /// Attempts to compute a canonical logical path and name for the given URL string
    /// according to the configured <see cref="Options"/>, without throwing when the
    /// input cannot be parsed or is otherwise invalid.
    /// </summary>
    /// <param name="url">The URL string to analyze.</param>
    /// <param name="result">
    /// When this method returns true, contains a <see cref="UrlNamingResult"/>
    /// describing the derived path, name and related metadata. When false,
    /// the value is undefined and should be ignored.
    /// </param>
    /// <returns>
    /// True if a path and name could be derived successfully; otherwise false.
    /// </returns>
    bool TryGetNameAndPath(string url, out UrlNamingResult? result);
}