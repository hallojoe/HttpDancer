using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;

namespace HttpDancer.FileSystemDownloader.Data;

/// <summary>
/// Default implementation of <see cref="IHtmlStringsProvider"/> using AngleSharp 1.4.
/// 
/// Behavior:
/// - Respects <see cref="HtmlParsingSettings.Enabled"/>.
/// - For each <see cref="HtmlStringQuery"/>, treats the Selectors as ordered fallbacks:
///   * Each selector is evaluated independently on the document root.
///   * Processing stops at the first selector that yields a match; later selectors are ignored.
/// - The value for each HtmlStringQuery.Alias is the trimmed text-content of the first matched element.
///   If nothing matches, the value is null.
/// </summary>
public sealed class AngleSharpHtmlStringsProvider : IHtmlStringsProvider
{
    private readonly HtmlParsingSettings _settings;
    private readonly HtmlParser _parser;

    /// <summary>
    /// DI-friendly constructor. 
    /// </summary>
    public AngleSharpHtmlStringsProvider(IOptions<HtmlParsingSettings> options)
        : this(options.Value ?? throw new ArgumentNullException(nameof(options)))
    {
    }

    /// <summary>
    /// Direct constructor for tests or manual wiring.
    /// </summary>
    public AngleSharpHtmlStringsProvider(HtmlParsingSettings settings, HtmlParser? parser = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _parser = parser ?? new HtmlParser();
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string?>> GetAsync(
        string utf8EncodedHtmlString,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodedHtmlString);

        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // If disabled or no map actions, then return an empty dictionary.
        if (!_settings.Enabled || _settings.Queries.Length == 0)
        {
            return result;
        }

        // Parse HTML with AngleSharp
        var document = await _parser
            .ParseDocumentAsync(utf8EncodedHtmlString, cancellationToken)
            .ConfigureAwait(false);

        // Prefer body if present, fall back to document element
        var root = document.Body ?? document.DocumentElement 
            ?? throw new InvalidOperationException("HTML document did not contain a body or document element.");

        foreach (var mapAction in _settings.Queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var key = mapAction.Alias.Trim();
            if (string.IsNullOrEmpty(key))
            {
                // No key = nothing to store
                continue;
            }

            // Default to null; we'll override if we find something.
            string? value = null;

            var selectorChain = mapAction.Selectors;
            if (selectorChain.Length == 0)
            {
                result[key] = null;
                continue;
            }

            IElement? matchedElement = null;

            // Walk selectors as fallbacks: stop as soon as one matches
            foreach (var rawSelector in selectorChain)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var selector = rawSelector.Trim();
                if (string.IsNullOrEmpty(selector))
                {
                    continue;
                }

                var matches = root.QuerySelectorAll(selector);
                if (matches.Length > 0)
                {
                    matchedElement = matches[0];
                    break;
                }
            }

            // Take the first match (if any) and use its text content
            if (matchedElement != null)
            {
                var text = matchedElement.TextContent.Trim();
                value = string.IsNullOrEmpty(text) ? null : text;
            }

            result[key] = value;
        }

        return result;
    }
}
