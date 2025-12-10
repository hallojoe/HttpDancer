using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace HttpDancer.FileSystemDownloader.Minification;

/// <summary>
/// HTML htmlMinifier powered by AngleSharp. Removes comments, collapses/normalizes whitespace,
/// trims text nodes, prunes empty elements (when safe), and optionally removes elements
/// matching user-provided CSS selectors.
/// </summary>
public class AngleSharpHtmlHtmlMinifier : IHtmlMinifier
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area","base","br","col","embed","hr","img","input","link","meta",
        "param","source","track","wbr"
    };

    public string Minify(string utf8EncodedHtmlString, IEnumerable<string>? selectorsToRemove = null)
    {
        if (string.IsNullOrWhiteSpace(utf8EncodedHtmlString))
        {
            return string.Empty;
        }

        IHtmlDocument document;
        try
        {
            var parser = new HtmlParser(new HtmlParserOptions
            {
                IsEmbedded = true,
                IsStrictMode = false
            });

            document = parser.ParseDocument(utf8EncodedHtmlString);
        }
        catch (Exception)
        {
            return utf8EncodedHtmlString;
        }

        try
        {
            RemoveBySelector(document, selectorsToRemove);
            RemoveComments(document);
            NormalizeTextNodes(document);
            RemoveEmptyElements(document);
        }
        catch (Exception)
        {
            return utf8EncodedHtmlString;
        }

        return document.ToHtml(new MinifyMarkupFormatter());
    }

    private static void RemoveBySelector(IHtmlDocument document, IEnumerable<string>? selectorsToRemove)
    {
        if (selectorsToRemove is null)
        {
            return;
        }

        foreach (var selector in selectorsToRemove)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                continue;
            }

            IHtmlCollection<IElement> nodes;
            try
            {
                nodes = document.QuerySelectorAll(selector);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var node in nodes.ToArray())
            {
                node.Remove();
            }
        }
    }

    private static void RemoveComments(INode root)
    {
        foreach (var comment in root.Descendants<IComment>().ToArray())
        {
            comment.Remove();
        }
    }

    private static void NormalizeTextNodes(INode root)
    {
        foreach (var textNode in root.Descendants<IText>().ToArray())
        {
            if (textNode.ParentElement is { TagName: var tag } &&
                (tag.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase) ||
                 tag.Equals("STYLE", StringComparison.OrdinalIgnoreCase)))
            {
                continue; // Do not touch script/style content.
            }

            var normalized = CollapseWhitespace(textNode.Data);
            textNode.Data = normalized;
        }
    }

    private static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var inWhitespace = false;

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (inWhitespace)
                {
                    continue;
                }
                builder.Append(' ');
                inWhitespace = true;
            }
            else
            {
                builder.Append(ch);
                inWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static void RemoveEmptyElements(IHtmlDocument document)
    {
        var structural = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "html", "head", "body" };

        // Walk from deepest to root to avoid leaving orphans.
        foreach (var element in document.All.Reverse())
        {
            if (VoidElements.Contains(element.TagName) || structural.Contains(element.TagName))
            {
                continue;
            }

            var hasAttributes = element.Attributes.Length > 0;
            var hasChildren = element.Children.Length > 0;
            var hasText = !string.IsNullOrWhiteSpace(element.TextContent);

            if (hasAttributes || hasChildren || hasText)
            {
                continue;
            }

            element.Remove();
        }
    }
}
