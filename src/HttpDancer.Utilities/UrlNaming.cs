using System.Net;
using System.Text.RegularExpressions;

namespace HttpDancer.Utilities;

public partial class UrlNaming
{

    /// <summary>
    /// Creates a normalized name like string from a URL.
    /// 
    /// Example:
    /// 
    ///   https://www.example.com/da/Organdonation/Godt-at-vide-om-organdonation/Donation-efter-cirkulatorisk-doed
    ///   => "godt-at-vide-om-organdonation"
    ///
    ///   /-/media/Organdonation/TO-DOOO-Organdonation-Illustration_1600pixel.ashx?h=791&iar=0&w=1600&hash=...
    /// 
    ///   When includeQueryString is true:
    ///   "to-dooo-organdonation-illustration-1600pixel.ashx.h.791.iar.0.w.1600.hash.9c1f387e31b796db77c736d0c843636a"
    ///
    /// When includeQueryString is false:
    ///   "to-dooo-organdonation-illustration-1600pixel.ashx"
    /// 
    /// </summary>
    /// <param name="url">Relative or absolute URL.</param>
    /// <param name="includeQueryString">Include querystring as dot seperated string.</param>
    /// <param name="baseUrl">Optional: ?</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GetName(string url, bool includeQueryString, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        var uri = BuildUri(url, baseUrl);
        var path = uri.AbsolutePath; // no query/fragment

        // Split into segments (ignore empty due to leading/trailing slashes)
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        // Decide which segment to use for the name:
        // - If the last segment looks like a file (contains '.'), use that.
        // - Otherwise use the second last segment (matches your "Godt-at-vide..." example).
        
        // Take last
        var nameSegment = segments[^1];
        
        // if (segments.Length == 1)
        // {
        //     nameSegment = segments[0];
        // }
        // else
        // {
        //     var last = segments[^1];
        //     nameSegment = last.Contains('.') ? last : segments[^2];
        // }

        var baseName = NormalizeSegmentForSlug(nameSegment);

        if (!includeQueryString || string.IsNullOrEmpty(uri.Query))
        {
            return baseName;
        }

        // Flatten query string into ".key.value" parts
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return baseName;
        }

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var suffix = string.Empty;

        foreach (var part in parts)
        {
            var kvp = part.Split(['='], 2);
            var keyRaw = kvp.Length > 0 ? kvp[0] : string.Empty;
            var valueRaw = kvp.Length > 1 ? kvp[1] : string.Empty;

            var key = NormalizeToken(keyRaw);
            var value = NormalizeToken(valueRaw);

            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (string.IsNullOrEmpty(value))
            {
                suffix += $".{key}";
            }
            else
            {
                suffix += $".{key}.{value}";
            }
        }

        return baseName + suffix;
    }
    
    /// <summary>
    /// Returns a normalized directory-style path, with excluded parts removed.
    ///
    /// Rules:
    /// - Normalizes '//' to '/'
    /// - Removes the filename (keeps directory path)
    /// - Applies excludedParts both as segment-level patterns and multi-segment patterns
    /// - All lowercase, segments normalized (spaces/underscore -> '-', collapse '--' to '-')
    /// - Returns path with leading and trailing '/'
    ///
    /// Examples:
    ///   Path: /da/-/media/folder/xxx.ext?query=0, excludedParts ["-/media", "da"] => "folder"
    ///   Path: /da/-/media/folder/xxx.ext?query=0, excludedParts ["da/-/media/"]    => "folder"
    /// </summary>
    /// <param name="url"></param>
    /// <param name="excludedParts"></param>
    /// <param name="baseUrl"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GetPath(string url, string[] excludedParts, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }
        
        var uri = BuildUri(url, baseUrl);
        var path = uri.AbsolutePath;

        // Normalize slashes and backslashes
        path = path.Replace('\\', '/');
        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        // Remove filename, keep directory only
        if (!path.EndsWith("/", StringComparison.Ordinal))
        {
            var lastSlash = path.LastIndexOf('/');
            path = lastSlash >= 0 ? path.Substring(0, lastSlash + 1) : "/";
        }

        path = path.ToLowerInvariant();

        // Apply excluded parts (segment-level and multi-segment)
        path = ApplyExcludedParts(path, excludedParts);

        // Normalize segments (spaces/_ -> '-', '--' -> '-', etc.)
        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        var normalizedSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            var normalized = NormalizeSegmentForSlug(segment);
            if (!string.IsNullOrEmpty(normalized))
            {
                normalizedSegments.Add(normalized);
            }
        }

        if (normalizedSegments.Count == 0)
        {
            return "/";
        }

        var result = "/" + string.Join('/', normalizedSegments) + "/";

        // Final double-slash normalization just in case
        while (result.Contains("//", StringComparison.Ordinal))
        {
            result = result.Replace("//", "/", StringComparison.Ordinal);
        }

        return result;
    }

    #region Helpers

    private static Uri BuildUri(string url, string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var baseUri = new Uri(baseUrl, UriKind.Absolute);
            return new Uri(baseUri, url);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        // Relative URL without the base URL – use substitute host, we only care about path and query.
        var substitute = url.StartsWith('/')
            ? "http://a" + url
            : "http://a/" + url;

        return new Uri(substitute, UriKind.Absolute);
    }

    /// <summary>
    /// Normalizes a path/name segment to a "slug-like" form:
    /// - URL-decodes
    /// - Trim whitespace
    /// - Lowercase
    /// - Spaces and '_' -> '-'
    /// - Collapse multiple '-' into one
    /// </summary>
    private static string NormalizeSegmentForSlug(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        var decoded = WebUtility.UrlDecode(segment);
        var s = decoded.Trim().ToLowerInvariant();

        s = Path.GetInvalidFileNameChars()
            .Aggregate(
                WebUtility.UrlDecode(s), 
                (current, c) => current.Replace(c, '-')
            );

        // Replace spaces and underscores with '-'
        s = s.Replace(' ', '-').Replace('_', '-');

        // Collapse multiple dashes
        s = DoubleDashExpression().Replace(s, "-");

        return s;
    }

    /// <summary>
    /// Normalizes a token used for query flattening:
    /// same rules as segments, but safe for use as a dot-separated part.
    /// </summary>
    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return string.Empty;
        }

        var decoded = WebUtility.UrlDecode(token);
        var s = decoded.Trim().ToLowerInvariant();

        s = s.Replace(' ', '-').Replace('_', '-');
        s = DoubleDashExpression().Replace(s, "-");

        return s;
    }

    /// <summary>
    /// Applies excludedParts to a directory-like path (always lowercase here).
    /// Handles:
    /// - multi-segment patterns like "da/-/media/"
    /// - single segment patterns like "da" or "-/media"
    /// </summary>
    private static string ApplyExcludedParts(string path, string[] excludedParts)
    {
        var working = path;

        foreach (var rawPattern in excludedParts)
        {
            if (string.IsNullOrWhiteSpace(rawPattern))
            {
                continue;
            }

            var pattern = rawPattern.Trim().Trim('/');
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            var patternLower = pattern.ToLowerInvariant();

            if (patternLower.Contains('/'))
            {
                // Multi-segment pattern: treat as "/pattern/"
                var patternToRemove = "/" + patternLower.Trim('/') + "/";
                working = working.Replace(patternToRemove, "/", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Single segment: remove "/segment/" occurrences
                var segmentPattern = "/" + patternLower + "/";
                working = Regex.Replace(
                    working,
                    Regex.Escape(segmentPattern),
                    "/",
                    RegexOptions.IgnoreCase);
            }

            // Normalize any accidental double slashes
            while (working.Contains("//", StringComparison.Ordinal))
            {
                working = working.Replace("//", "/", StringComparison.Ordinal);
            }
        }

        return working;
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex DoubleDashExpression();

    #endregion
}