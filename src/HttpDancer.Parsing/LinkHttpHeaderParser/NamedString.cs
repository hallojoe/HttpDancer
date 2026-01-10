namespace HttpDancer.Parsing.LinkHttpHeaderParser;

/// <summary>
/// Simple "name/value" pair. Matches the user's shape:
/// NamedString&lt;string, string&gt;(string Name, string Value).
/// </summary>
public readonly record struct NamedString(string Name, string Value);