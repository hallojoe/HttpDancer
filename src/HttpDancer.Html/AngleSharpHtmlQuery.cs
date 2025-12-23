using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace HttpDancer.Html;

/// <summary>
/// AngleSharp-powered implementation of <see cref="IHtmlQuery"/> that supports
/// ordered selector fallbacks and returns rich element metadata.
/// </summary>
public sealed class AngleSharpHtmlQuery : IHtmlQuery
{
    private static readonly HtmlParserOptions ParserOptions = new()
    {
        IsEmbedded = true,
        IsStrictMode = false
    };

    public async Task<IUtf8EncodedHtmlString> QueryAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);

        var results = await QueryAllAsync(utf8EncodeHtmlString, selectors, valueStrategy, attributeName, cancellationToken)
            .ConfigureAwait(false);

        return results.FirstOrDefault() ?? CreateEmptyResult();
    }

    public Task<IUtf8EncodedHtmlString> QueryAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);
        return QueryAsync(utf8EncodeHtmlString.Value, selectors, valueStrategy, attributeName, cancellationToken);
    }

    public async Task<IUtf8EncodedHtmlString[]> QueryAllAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);

        var document = await ParseDocumentAsync(utf8EncodeHtmlString, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return [];
        }

        var normalizedSelectors = NormalizeSelectors(selectors);
        if (normalizedSelectors.Length == 0)
        {
            return [];
        }

        foreach (var selector in normalizedSelectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            IHtmlCollection<IElement> matches;
            try
            {
                matches = document.QuerySelectorAll(selector);
            }
            catch (Exception)
            {
                continue; // Invalid selector; skip to the next one.
            }

            if (matches.Length == 0)
            {
                continue;
            }

            var mapped = new List<IUtf8EncodedHtmlString>(matches.Length);
            foreach (var element in matches)
            {
                mapped.Add(MapElement(element, selector, valueStrategy, attributeName));
            }

            return mapped.ToArray();
        }

        return [];
    }

    public Task<IUtf8EncodedHtmlString[]> QueryAllAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);
        return QueryAllAsync(utf8EncodeHtmlString.Value, selectors, valueStrategy, attributeName, cancellationToken);
    }

    public async Task<IUtf8EncodedHtmlString> RemoveQueryAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);

        var document = await ParseDocumentAsync(utf8EncodeHtmlString, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return CreateEmptyResult(utf8EncodeHtmlString);
        }

        var normalizedSelectors = NormalizeSelectors(selectors);
        if (normalizedSelectors.Length == 0)
        {
            return new Utf8EncodedHtmlString
            {
                Selector = null,
                TagName = null,
                Attributes = [],
                Value = utf8EncodeHtmlString
            };
        }

        foreach (var selector in normalizedSelectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(selector))
            {
                continue;
            }

            IHtmlCollection<IElement> matches;
            try
            {
                matches = document.QuerySelectorAll(selector);
            }
            catch (Exception)
            {
                continue; // Skip invalid selectors.
            }

            if (matches.Length == 0)
            {
                continue;
            }

            foreach (var match in matches.ToArray())
            {
                match.Remove();
            }
        }

        return new Utf8EncodedHtmlString
        {
            Selector = string.Join(", ", normalizedSelectors),
            TagName = null,
            Attributes = [],
            Value = RenderPreservingFragment(utf8EncodeHtmlString, document, new HtmlMarkupFormatter())
        };
    }

    public async Task<IUtf8EncodedHtmlString[]> MinifyQueryAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);

        var results = await QueryAllAsync(utf8EncodeHtmlString, selectors, valueStrategy, attributeName, cancellationToken)
            .ConfigureAwait(false);

        if (results.Length == 0 || valueStrategy is HtmlValueStrategy.Text or HtmlValueStrategy.Attribute)
        {
            return results;
        }

        var minified = new IUtf8EncodedHtmlString[results.Length];
        for (var index = 0; index < results.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = results[index];
            var minifiedValue = await MinifyFragmentAsync(current.Value, cancellationToken).ConfigureAwait(false);

            minified[index] = new Utf8EncodedHtmlString
            {
                Selector = current.Selector,
                TagName = current.TagName,
                Attributes = current.Attributes,
                Value = minifiedValue
            };
        }

        return minified;
    }

    private static Utf8EncodedHtmlString MapElement(IElement element, string selectorUsed, HtmlValueStrategy valueStrategy, string attributeName)
    {
        var attributes = element.Attributes
            .Select(attribute => new KeyValuePair<string, string?>(attribute.Name, attribute.Value))
            .ToArray();

        var value = ExtractValue(element, valueStrategy, attributeName);

        return new Utf8EncodedHtmlString
        {
            Selector = selectorUsed,
            TagName = element.TagName,
            Value = value,
            Attributes = attributes
        };
    }

    private static string ExtractValue(IElement element, HtmlValueStrategy valueStrategy, string attributeName)
    {
        var attributeKey = string.IsNullOrWhiteSpace(attributeName) ? "content" : attributeName;

        return valueStrategy switch
        {
            HtmlValueStrategy.InnerHtml => element.InnerHtml,
            HtmlValueStrategy.Text => element.TextContent,
            HtmlValueStrategy.Attribute => element.GetAttribute(attributeKey) ?? string.Empty,
            _ => element.OuterHtml
        };
    }

    private static string?[] NormalizeSelectors(IEnumerable<string>? selectors)
    {
        return selectors?
            .Select(selector => selector.Trim())
            .Where(selector => !string.IsNullOrEmpty(selector))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static Utf8EncodedHtmlString CreateEmptyResult(string value = "")
    {
        return new Utf8EncodedHtmlString
        {
            Selector = null,
            TagName = null,
            Attributes = [],
            Value = value
        };
    }

    private static async Task<IHtmlDocument?> ParseDocumentAsync(
        string html,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        try
        {
            var parser = CreateParser();
            return await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static HtmlParser CreateParser() => new(ParserOptions);

    private static async Task<string> MinifyFragmentAsync(
        string htmlFragment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(htmlFragment))
        {
            return string.Empty;
        }
    
        try
        {
            var parser = CreateParser();
            var document = await parser.ParseDocumentAsync(htmlFragment, cancellationToken)
                .ConfigureAwait(false);
    
            var formatter = new MinifyMarkupFormatter();
            return RenderPreservingFragment(htmlFragment, document, formatter);
        }
        catch (Exception)
        {
            return htmlFragment.Trim();
        }
    }

    private static string RenderHtml(INode node, AngleSharp.IMarkupFormatter formatter)
    {
        using var writer = new StringWriter();
        node.ToHtml(writer, formatter);
        return writer.ToString();
    }
    
    public Task<IUtf8EncodedHtmlString> RemoveAttributesAsync(
        string utf8EncodeHtmlString,
        string[]? attributeNames = null,
        string[]? preservedTagNames = null,
        CancellationToken cancellationToken = default)
    {
        return RemoveAttributesInternalAsync(utf8EncodeHtmlString, attributeNames, preservedTagNames, cancellationToken);
    }

    public Task<IUtf8EncodedHtmlString> RemoveAttributesAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[]? attributeNames = null,
        string[]? preservedTagNames = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);
        return RemoveAttributesInternalAsync(
            utf8EncodeHtmlString.Value,
            attributeNames,
            preservedTagNames,
            cancellationToken);
    }

    public async Task<IUtf8EncodedHtmlString> MakeLinksAbsoluteAsync(
        string utf8EncodeHtmlString,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);
        ArgumentNullException.ThrowIfNull(baseUrl);

        var document = await ParseDocumentAsync(utf8EncodeHtmlString, cancellationToken).ConfigureAwait(false);
        if (document is null || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return CreateEmptyResult(utf8EncodeHtmlString);
        }

        foreach (var element in document.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UpdateAttributeToAbsolute(element, "href", baseUri);
            UpdateAttributeToAbsolute(element, "src", baseUri);
        }

        return new Utf8EncodedHtmlString
        {
            Selector = null,
            TagName = null,
            Attributes = [],
            Value = RenderPreservingFragment(utf8EncodeHtmlString, document, new HtmlMarkupFormatter())
        };
    }

    public Task<IUtf8EncodedHtmlString> MakeLinksAbsoluteAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);
        return MakeLinksAbsoluteAsync(utf8EncodeHtmlString.Value, baseUrl, cancellationToken);
    }

    private static void UpdateAttributeToAbsolute(IElement element, string attributeName, Uri baseUri)
    {
        var value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var attributeUri))
        {
            return;
        }

        if (!attributeUri.IsAbsoluteUri)
        {
            var combined = new Uri(baseUri, attributeUri);
            element.SetAttribute(attributeName, combined.ToString());
        }
    }

    private static string RenderPreservingFragment(string originalHtml, IDocument document, AngleSharp.IMarkupFormatter formatter)
    {
        if (ContainsHtmlRoot(originalHtml))
        {
            return RenderHtml(document, formatter);
        }

        if (document.Body is { } body)
        {
            if (body.ChildElementCount == 1 && body.FirstElementChild is not null)
            {
                return RenderHtml(body.FirstElementChild, formatter);
            }

            return body.InnerHtml;
        }

        return RenderHtml(document, formatter);
    }

    private static bool ContainsHtmlRoot(string value)
    {
        return value.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private async Task<IUtf8EncodedHtmlString> RemoveAttributesInternalAsync(
        string utf8EncodeHtmlString,
        string[]? attributeNames,
        string[]? preservedTagNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodeHtmlString);

        var document = await ParseDocumentAsync(utf8EncodeHtmlString, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return CreateEmptyResult(utf8EncodeHtmlString);
        }

        RemoveAttributesFromDocument(
            document,
            ToSet(attributeNames),
            ToSet(preservedTagNames),
            cancellationToken);

        return new Utf8EncodedHtmlString
        {
            Selector = null,
            TagName = null,
            Attributes = [],
            Value = RenderPreservingFragment(utf8EncodeHtmlString, document, new HtmlMarkupFormatter())
        };
    }

    private static void RemoveAttributesFromDocument(
        IDocument document,
        HashSet<string>? attributesToRemove,
        HashSet<string>? preservedTagNames,
        CancellationToken cancellationToken)
    {
        foreach (var element in document.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (preservedTagNames is not null && preservedTagNames.Contains(element.TagName))
            {
                continue;
            }

            if (element.Attributes.Length == 0)
            {
                continue;
            }

            foreach (var attribute in element.Attributes.ToArray())
            {
                if (attributesToRemove is null || attributesToRemove.Contains(attribute.Name))
                {
                    element.RemoveAttribute(attribute.Name);
                }
            }
        }
    }

    private static HashSet<string>? ToSet(string[]? values)
    {
        if (values is not { Length: > 0 })
        {
            return null;
        }

        return new HashSet<string>(
            values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }
}
