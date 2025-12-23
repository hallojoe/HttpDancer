namespace HttpDancer.Core.Naming.Segments;

/// <summary>
/// Normalizes individual path or name segments according to configured strategies.
/// </summary>
public interface IPathSegmentNormalizer
{
    string NormalizeSegment(string segment, UrlNamingOptions options);

    string NormalizeToken(string token, UrlNamingOptions options);
}
