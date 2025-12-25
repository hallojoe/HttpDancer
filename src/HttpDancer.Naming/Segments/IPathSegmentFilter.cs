namespace HttpDancer.Naming.Segments;

/// <summary>
/// Removes configured path fragments before further URL processing.
/// </summary>
public interface IPathSegmentFilter
{
    string SanitizePath(string rawPath, string[] excludedSegments);
}
