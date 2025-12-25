using HttpDancer.Naming.Enums;
using HttpDancer.Naming.Hashing;
using HttpDancer.Naming.Segments;

namespace HttpDancer.Naming.Query;

public class QueryStringProcessor(IPathSegmentNormalizer pathSegmentNormalizer, IHashGenerator hashGenerator) : IQueryStringProcessor
{
    public QueryProcessingResult Process(Uri uri, UrlNamingOptions options)
    {
        if (options.QueryStringStrategy == QueryStringStrategy.Ignore)
        {
            return new QueryProcessingResult();
        }

        var rawQuery = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new QueryProcessingResult();
        }

        var queryItems = ParseQuery(rawQuery, options);
        if (queryItems.Count == 0)
        {
            return new QueryProcessingResult();
        }

        var normalizedPairs = NormalizePairs(queryItems, options);

        return options.QueryStringStrategy switch
        {
            QueryStringStrategy.Append => BuildAppendResult(normalizedPairs, options),
            QueryStringStrategy.Hash => BuildHashResult(normalizedPairs, options),
            _ => new QueryProcessingResult()
        };
    }

    private static List<KeyValuePair<string, string>> ParseQuery(string query, UrlNamingOptions options)
    {
        var ignoredKeys = new HashSet<string>(options.IgnoredQueryKeys ?? [], StringComparer.OrdinalIgnoreCase);
        var pairs = new List<KeyValuePair<string, string>>();
        var segments = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var keyValue = segment.Split(['='], 2);
            var key = keyValue.Length > 0 ? keyValue[0] : string.Empty;

            if (ignoredKeys.Contains(key))
            {
                continue;
            }

            var value = keyValue.Length > 1 ? keyValue[1] : string.Empty;
            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        return pairs;
    }

    private List<KeyValuePair<string, string>> NormalizePairs(IEnumerable<KeyValuePair<string, string>> pairs, UrlNamingOptions options)
    {
        var normalized = new List<KeyValuePair<string, string>>();

        foreach (var pair in pairs)
        {
            var key = pair.Key;
            var value = pair.Value;

            if (options.QueryParts.HasFlag(QueryParts.Lowercase))
            {
                key = key.ToLowerInvariant();
                value = value.ToLowerInvariant();
            }

            var normalizedKey = pathSegmentNormalizer.NormalizeToken(key, options);
            var normalizedValue = pathSegmentNormalizer.NormalizeToken(value, options);

            normalized.Add(new KeyValuePair<string, string>(normalizedKey, normalizedValue));
        }

        if (options.QueryParts.HasFlag(QueryParts.SortKeys))
        {
            normalized = normalized
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .ToList();
        }

        return normalized;
    }

    private QueryProcessingResult BuildAppendResult(IEnumerable<KeyValuePair<string, string>> normalizedPairs, UrlNamingOptions options)
    {
        var parts = new List<string>();

        foreach (var pair in normalizedPairs)
        {
            if (options.QueryParts.HasFlag(QueryParts.Keys) && !string.IsNullOrEmpty(pair.Key))
            {
                parts.Add(pair.Key);
            }

            if (options.QueryParts.HasFlag(QueryParts.Values) && !string.IsNullOrEmpty(pair.Value))
            {
                parts.Add(pair.Value);
            }
        }

        if (parts.Count == 0)
        {
            return new QueryProcessingResult();
        }

        var appendString = string.Join(".", parts);
        return new QueryProcessingResult { AppendedName = appendString };
    }

    private QueryProcessingResult BuildHashResult(IEnumerable<KeyValuePair<string, string>> normalizedPairs, UrlNamingOptions options)
    {
        var canonical = string.Join("&", normalizedPairs.Select(pair => $"{pair.Key}={pair.Value}"));
        if (string.IsNullOrEmpty(canonical))
        {
            return new QueryProcessingResult();
        }

        var hash = hashGenerator.GenerateHash(canonical, minLength: 11);
        return new QueryProcessingResult { QueryHash = hash };
    }
}
