using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using HttpDancer.Extensions;

namespace HttpDancer.Parsing;

/// <summary>
/// Extracts navigable HTTP(S) links from real HTML elements instead of scanning
/// script or JSON text for strings that merely resemble URLs.
/// </summary>
public sealed class AngleSharpLinkParser : ILinkParser
{
    private const string UrlElementSelector =
        "a[href], area[href], link[href], script[src], img[src], iframe[src], embed[src], " +
        "source[src], audio[src], video[src], track[src], object[data], form[action]";

    public Link[] GetLinks(string utf8EncodedString, string sourceUrl, string? urlFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(utf8EncodedString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var baseUri))
            throw new ArgumentException("Source URL must be an absolute URI.", nameof(sourceUrl));

        var document = new HtmlParser().ParseDocument(utf8EncodedString);
        var links = new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.QuerySelectorAll(UrlElementSelector))
        {
            var attributeName = GetUrlAttributeName(element);
            AddLink(element.GetAttribute(attributeName), baseUri, sourceUrl, urlFilter, links);
        }

        foreach (var element in document.QuerySelectorAll("img[srcset], source[srcset]"))
        {
            var srcset = element.GetAttribute("srcset");
            if (string.IsNullOrWhiteSpace(srcset)) continue;

            foreach (var candidate in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var source = candidate.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                AddLink(source, baseUri, sourceUrl, urlFilter, links);
            }
        }

        return links.Values.ToArray();
    }

    private static string GetUrlAttributeName(IElement element) => element.LocalName switch
    {
        "object" => "data",
        "form" => "action",
        _ => element.HasAttribute("href") ? "href" : "src"
    };

    private static void AddLink(
        string? value,
        Uri baseUri,
        string sourceUrl,
        string? urlFilter,
        IDictionary<string, Link> links)
    {
        if (!TryResolve(value, baseUri, out var uri)) return;

        var isAllowed = urlFilter is null
            ? uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            : uri.AbsoluteUri.IsMatch(urlFilter);
        if (!isAllowed) return;

        links.TryAdd(uri.AbsoluteUri, new Link
        {
            SourceUrl = sourceUrl,
            RawUrl = value!,
            Uri = uri
        });
    }

    private static bool TryResolve(string? value, Uri baseUri, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var raw = WebUtility.HtmlDecode(value).Trim();
        if (raw.Length == 0 || raw[0] == '#' || raw.Any(char.IsWhiteSpace) || raw.Contains('"') || raw.Contains('\''))
            return false;

        Uri? resolved;
        if (raw.StartsWith("/", StringComparison.Ordinal))
            resolved = new Uri(baseUri, raw);
        else if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute))
        {
            if (!absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;
            resolved = absolute;
        }
        else if (Uri.TryCreate(baseUri, raw, out var relative))
            resolved = relative;
        else
            return false;

        uri = resolved;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
