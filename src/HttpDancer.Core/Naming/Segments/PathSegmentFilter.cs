using System.Text;

namespace HttpDancer.Core.Naming.Segments;

/// <summary>
/// Removes configured path fragments (case-insensitive) by simple replacement,
/// then collapses redundant slashes to produce a clean path.
/// </summary>
public class PathSegmentFilter : IPathSegmentFilter
{
    public string SanitizePath(string rawPath, string[] excludedSegments)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return "/";
        }

        var workingPath = rawPath.Replace('\\', '/');

        foreach (var excludedSegment in excludedSegments)
        {
            if (string.IsNullOrWhiteSpace(excludedSegment))
            {
                continue;
            }

            var trimmedSegment = excludedSegment.Trim();
            if (trimmedSegment.Length == 0)
            {
                continue;
            }

            workingPath = workingPath.Replace(trimmedSegment, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return NormalizeSlashes(workingPath);
    }

    private static string NormalizeSlashes(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSlash = false;

        foreach (var character in value)
        {
            if (character == '/')
            {
                if (previousWasSlash)
                {
                    continue;
                }

                previousWasSlash = true;
                builder.Append(character);
                continue;
            }

            previousWasSlash = false;
            builder.Append(character);
        }

        var normalized = builder.ToString();

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }
}
