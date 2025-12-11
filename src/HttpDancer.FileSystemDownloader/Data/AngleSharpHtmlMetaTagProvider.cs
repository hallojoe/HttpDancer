using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace HttpDancer.FileSystemDownloader.Data;

public class AngleSharpHtmlMetaTagProvider : IHtmlMetaTagProvider
{
    private static readonly HashSet<string> KnownAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "content",
        "http-equiv",
        "charset",
        "property",
        "itemprop"
    };
    
    /// <summary>
    /// Parses all &lt;meta&gt; tags in &lt;head&gt; and &lt;body&gt; from an HTML string.
    /// </summary>
    /// <param name="utf8EncodedHtmlString">The HTML source.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of HtmlMetaTag objects representing all meta-tags.</returns>
    public async Task<HtmlMetaTag[]> GetAsync(string? utf8EncodedHtmlString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(utf8EncodedHtmlString))
        {
            return [];
        }

        IHtmlDocument document;
        try
        {
            var parser = new HtmlParser(new()
            {
                IsEmbedded = true,
                IsStrictMode = false
            });

            document = await parser.ParseDocumentAsync(utf8EncodedHtmlString, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }

        try
        {
            // Select all <meta> tags in head and body
            var headMetaElements = document.Head?.QuerySelectorAll("meta");
            var bodyMetaElements = document.Body?.QuerySelectorAll("meta");
            var allMetaElements = new List<IElement>();
            if(headMetaElements?.Count > 0) allMetaElements.AddRange(headMetaElements);
            if(bodyMetaElements?.Count > 0) allMetaElements.AddRange(bodyMetaElements);
            if (allMetaElements.Count == 0)
            {
                return [];
            }

            var result = new List<HtmlMetaTag>(allMetaElements.Count);

            foreach (var metaElement in allMetaElements)
            {
                var meta = new HtmlMetaTag
                {
                    Name      = GetAttribute(metaElement, "name"),
                    Content   = GetAttribute(metaElement, "content"),
                    HttpEquiv = GetAttribute(metaElement, "http-equiv"),
                    Charset   = GetAttribute(metaElement, "charset"),
                    Property  = GetAttribute(metaElement, "property"),
                    ItemProp  = GetAttribute(metaElement, "itemprop"),
                    RawHtml   = metaElement.OuterHtml,
                    Location  = metaElement.GetAncestors().Any(element => element.NodeName.ToLowerInvariant().Equals("head")) ? "head" : "body"
                };

                // Capture any extra attributes
                foreach (var attribute in metaElement.Attributes)
                {
                    if (!KnownAttributes.Contains(attribute.Name))
                    {
                        meta.AdditionalAttributes[attribute.Name] = attribute.Value;
                    }
                }

                result.Add(meta);
            }

            return result.ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private string? GetAttribute(IElement element, string attributeName)
    {
        return element.Attributes[attributeName]?.Value;
    }
}
