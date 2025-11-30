using System.Net.Http.Headers;

namespace HttpDancer.Core.Extensions;

internal static class HttpResponseMessageExtensions
{
    internal static Dictionary<string, IReadOnlyList<string>> GetHeaders(this HttpResponseMessage response)
    {
        // Use case-insensitive key comparer (required for HTTP semantics)
        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        // Add response headers
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToList().AsReadOnly();
        }

        // Add content headers (if any)
        foreach (var header in response.Content.Headers)
        {
            if (headers.TryGetValue(header.Key, out var existing))
            {
                // Merge values in case the header exists in both collections
                var merged = existing.Concat(header.Value).ToList().AsReadOnly();
                headers[header.Key] = merged;
            }
            else
            {
                headers[header.Key] = header.Value.ToList().AsReadOnly();
            }
        }

        return headers;
    }
}