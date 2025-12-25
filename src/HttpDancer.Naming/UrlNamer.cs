using System.Globalization;
using HttpDancer.Naming.Enums;
using HttpDancer.Naming.Query;
using HttpDancer.Naming.Segments;
using Microsoft.Extensions.Options;

namespace HttpDancer.Naming;

public class UrlNamer(
    IOptions<UrlNamingOptions> urlNamingOptions,
    IPathSegmentFilter pathSegmentFilter,
    IPathSegmentNormalizer pathSegmentNormalizer,
    IQueryStringProcessor queryStringProcessor)
    : IUrlNamer
{
    public UrlNamingOptions Options => urlNamingOptions.Value;

    public UrlNamingResult GetNameAndPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        var uri = BuildUri(url);
        return GetNameAndPath(uri);
    }

    public UrlNamingResult GetNameAndPath(Uri uri)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        var currentOptions = Options;

        var adjustedPath = ApplyTrailingSlashStrategy(uri.AbsolutePath, currentOptions.TrailingSlash);
        var sanitizedPath = pathSegmentFilter.SanitizePath(adjustedPath, currentOptions.ExcludedPathSegments);
        var pathSegments = SplitSegments(sanitizedPath);
        var hasTrailingSlash = sanitizedPath.EndsWith("/", StringComparison.Ordinal);

        var pathSegmentsAfterIndexHandling = HandleIndexDocuments(pathSegments, currentOptions);
        var nameSegment = DetermineNameSegment(pathSegmentsAfterIndexHandling, hasTrailingSlash, currentOptions);
        var extensionAdjustedName = ApplyExtensionStrategy(nameSegment, currentOptions);

        var normalizedName = pathSegmentNormalizer.NormalizeSegment(extensionAdjustedName, currentOptions);
        if (string.IsNullOrEmpty(normalizedName) && currentOptions.RootNamingStrategy == RootNamingStrategy.UseFixedName)
        {
            normalizedName = currentOptions.FixedRootName;
        }

        var queryProcessing = queryStringProcessor.Process(uri, currentOptions);

        if (!string.IsNullOrEmpty(queryProcessing.AppendedName))
        {
            normalizedName = string.IsNullOrEmpty(normalizedName)
                ? queryProcessing.AppendedName!
                : $"{normalizedName}.{queryProcessing.AppendedName}";
        }

        if (!string.IsNullOrEmpty(queryProcessing.QueryHash) && currentOptions.QueryStringStrategy == QueryStringStrategy.Hash)
        {
            normalizedName = string.IsNullOrEmpty(normalizedName)
                ? queryProcessing.QueryHash!
                : $"{normalizedName}{currentOptions.ReplacementChar}{queryProcessing.QueryHash}";
        }

        if (normalizedName.Length > currentOptions.MaxNameLength)
        {
            normalizedName = normalizedName[..currentOptions.MaxNameLength];
        }

        var pathComponents = BuildPathComponents(
            uri,
            pathSegmentsAfterIndexHandling,
            hasTrailingSlash,
            queryProcessing,
            currentOptions);

        var normalizedPath = NormalizePath(pathComponents, currentOptions);

        return new UrlNamingResult
        {
            SourceUrl = uri.ToString(),
            Path = normalizedPath,
            Name = normalizedName,
            FullPath = BuildFullPath(normalizedPath, normalizedName),
            CanonicalUrl = uri.ToString(),
            QueryHash = queryProcessing.QueryHash
        };
    }

    public bool TryGetNameAndPath(string url, out UrlNamingResult? result)
    {
        result = null;
        try
        {
            result = GetNameAndPath(url);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Uri BuildUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (Uri.TryCreate(url, UriKind.Relative, out var relative))
        {
            return new Uri(new Uri("http://placeholder"), relative);
        }

        throw new ArgumentException("Invalid URL format.", nameof(url));
    }

    private static string ApplyTrailingSlashStrategy(string path, TrailingSlashStrategy strategy)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        return strategy switch
        {
            TrailingSlashStrategy.NormalizeToSlash => path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/",
            TrailingSlashStrategy.NormalizeWithoutSlash => path != "/" ? path.TrimEnd('/') : path,
            _ => path
        };
    }

    private static List<string> SplitSegments(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.ToList();
    }

    private static List<string> HandleIndexDocuments(List<string> segments, UrlNamingOptions options)
    {
        if (segments.Count == 0)
        {
            return segments;
        }

        var lastSegment = segments[^1];
        if (!IsDefaultDocument(lastSegment, options))
        {
            return segments;
        }

        if (options.IndexDocumentStrategy == IndexDocumentStrategy.TreatAsFolder)
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return segments;
    }

    private static bool IsDefaultDocument(string segment, UrlNamingOptions options)
    {
        var segmentWithoutExtension = Path.GetFileNameWithoutExtension(segment);
        return options.DefaultDocumentNames.Any(defaultName =>
            segmentWithoutExtension.Equals(defaultName, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetermineNameSegment(
        List<string> segments,
        bool hasTrailingSlash,
        UrlNamingOptions options)
    {
        if (options.TrailingSlash == TrailingSlashStrategy.TreatTrailingSlashAsIndex && hasTrailingSlash)
        {
            return options.FixedRootName;
        }

        if (segments.Count == 0)
        {
            return options.RootNamingStrategy switch
            {
                RootNamingStrategy.EmptyName => string.Empty,
                RootNamingStrategy.UseFixedName => options.FixedRootName,
                RootNamingStrategy.UseLastSegment => string.Empty,
                _ => string.Empty
            };
        }

        return segments[^1];
    }

    private static string ApplyExtensionStrategy(string nameSegment, UrlNamingOptions options)
    {
        var extension = Path.GetExtension(nameSegment);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(nameSegment);

        return options.ExtensionStrategy switch
        {
            ExtensionStrategy.KeepAll => nameSegment,
            ExtensionStrategy.StripKnownPageExtensions =>
                options.PageExtensions.Any(pageExtension => extension.Equals(pageExtension, StringComparison.OrdinalIgnoreCase))
                    ? nameWithoutExtension
                    : nameSegment,
            ExtensionStrategy.StripAll => nameWithoutExtension,
            ExtensionStrategy.PageVsAsset =>
                options.PageExtensions.Any(pageExtension => extension.Equals(pageExtension, StringComparison.OrdinalIgnoreCase))
                    ? nameWithoutExtension
                    : nameSegment,
            _ => nameSegment
        };
    }

    private List<string> BuildPathComponents(
        Uri uri,
        List<string> segments,
        bool hasTrailingSlash,
        QueryProcessingResult queryProcessingResult,
        UrlNamingOptions options)
    {
        var pathSegments = new List<string>();

        if (options.HostStrategy == HostStrategy.IncludeHost && !string.IsNullOrWhiteSpace(uri.Host))
        {
            pathSegments.Add(NormalizeHost(uri.Host, options));
        }

        if (segments.Count > 0)
        {
            var segmentCountToInclude = hasTrailingSlash
                ? segments.Count
                : Math.Max(segments.Count - 1, 0);

            for (var index = 0; index < segmentCountToInclude; index++)
            {
                pathSegments.Add(pathSegmentNormalizer.NormalizeSegment(segments[index], options));
            }
        }

        if (options.PathComponents.HasFlag(PathComponents.QueryHash) && !string.IsNullOrEmpty(queryProcessingResult.QueryHash))
        {
            pathSegments.Add(pathSegmentNormalizer.NormalizeSegment(queryProcessingResult.QueryHash!, options));
        }

        return pathSegments;
    }

    private static string NormalizePath(IEnumerable<string> segments, UrlNamingOptions _)
    {
        var normalizedSegments = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return string.Join('/', normalizedSegments);
    }

    private static string BuildFullPath(string path, string name)
    {
        if (string.IsNullOrEmpty(path))
        {
            return name;
        }

        if (string.IsNullOrEmpty(name))
        {
            return path;
        }

        return $"{path}/{name}";
    }

    private static string NormalizeHost(string host, UrlNamingOptions options)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        return options.CasingStrategy switch
        {
            CasingStrategy.Preserve => host,
            CasingStrategy.Lowercase => host.ToLower(CultureInfo.CurrentCulture),
            CasingStrategy.LowercaseInvariant => host.ToLowerInvariant(),
            _ => host
        };
    }
}
