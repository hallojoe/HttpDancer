namespace HttpDancer.Parsing.LinkHttpHeaderParser;

/// <summary>
/// Parsed Link header entry.
/// Href is the URI reference inside &lt;...&gt;.
/// Attributes are the parameters after the href, e.g. rel, as, fetchpriority, etc.
/// </summary>
public sealed record LinkHttpHeader(string Href, IReadOnlyList<NamedString> Attributes);