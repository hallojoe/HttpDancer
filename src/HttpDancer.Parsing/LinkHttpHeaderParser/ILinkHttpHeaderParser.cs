namespace HttpDancer.Parsing.LinkHttpHeaderParser;

public interface ILinkHttpHeaderParser
{
    /// <summary>
    /// Parse all "Link" headers in <paramref name="linkHeaderValues"/> into a flat list of LinkHttpHeader entries.
    /// </summary>
    IReadOnlyList<LinkHttpHeader> Parse(IReadOnlyList<string> linkHeaderValues);
}