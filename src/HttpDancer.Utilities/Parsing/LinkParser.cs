using System.Net;
using System.Text.RegularExpressions;

namespace HttpDancer.Utilities.Parsing;

public partial class LinkParser
{
    // Matches href="...", src='...', href=..., etc.
    // Groups:
    //  1: attribute name (href|src|...see expression)
    //  2: double-quoted value
    //  3: single-quoted value
    //  4: unquoted value

    public static Link[] GetLinks(string utf8EncodedString, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(utf8EncodedString))
        {
            throw new ArgumentNullException(nameof(utf8EncodedString));
        }

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException("Source URL is required.", nameof(sourceUrl));
        }

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var baseUri) is false)
        {
            throw new ArgumentException("Source URL must be an absolute URI.", nameof(sourceUrl));
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract from HTML-like attributes (href/src)
        foreach (Match match in HtmlAttributeUrlRegularExpression().Matches(utf8EncodedString))
        {
            var value = match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Success
                    ? match.Groups[2].Value
                    : match.Groups[3].Success
                        ? match.Groups[3].Value
                        : null;

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // HTML decode (e.g. &amp;, etc.)
            var decoded = WebUtility.HtmlDecode(value).Trim();

            if (!string.IsNullOrEmpty(decoded))
            {
                candidates.Add(decoded);
            }
        }

        // Extract from plain text
        foreach (Match match in PlainTextUrlRegularExpression().Matches(utf8EncodedString))
        {
            var value = match.Value.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                candidates.Add(value);
            }
        }

        // Resolve and validate
        var links = new List<Link>();

        foreach (var raw in candidates)
        {
            // Skip obvious
            if (raw.StartsWith('#'))
            {
                continue;
            }

            // Try absolute first
            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) is false)
            {
                // Try resolving as relative against base
                if (Uri.TryCreate(baseUri, raw, out uri) is false)
                {
                    // Not a valid URI - continue
                    continue;
                }
            }

            // Optional: only keep http/https
            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) is false&&
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            links.Add(new Link
            {
                SourceUrl = sourceUrl,
                RawUrl = raw,
                Uri = uri
            });
        }

        return links.ToArray();
    }

    [GeneratedRegex(
        @"(?i)\b(?:href|src|srcset|action|formaction|data|poster|cite|archive|codebase|longdesc|manifest|profile|icon|usemap)\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))",
        RegexOptions.Compiled)]
    private static partial Regex HtmlAttributeUrlRegularExpression();
    
    [GeneratedRegex(
        @"\bhttps?://[^\s""'<>]+", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlainTextUrlRegularExpression();
}
