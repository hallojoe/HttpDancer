using HttpDancer.Html;
using HttpDancer.Parsing;
using Microsoft.Extensions.Options;

namespace HttpDancer.FileSystemDownloader.Data;

/// <summary>
/// <see cref="IHtmlStringsProvider"/> implementation that relies on <see cref="IHtmlQuery"/>
/// to select elements and returns their value according to <see cref="HtmlValueStrategy"/>.
/// </summary>
public sealed class HtmlStringsParser(
    HtmlParsingSettings htmlParsingSettings,
    IHtmlQuery htmlQuery,
    IDateTimeParser dateTimeParser)
    : IHtmlStringsProvider
{
    private readonly HtmlParsingSettings _htmlParsingSettings = htmlParsingSettings ?? throw new ArgumentNullException(nameof(htmlParsingSettings));
    private readonly IHtmlQuery _htmlQuery = htmlQuery ?? throw new ArgumentNullException(nameof(htmlQuery));
    private readonly IDateTimeParser _dateTimeParser = dateTimeParser ?? throw new ArgumentNullException(nameof(dateTimeParser));

    public HtmlStringsParser(IOptions<HtmlParsingSettings> options, IHtmlQuery htmlQuery, IDateTimeParser dateTimeParser)
        : this(options.Value ?? throw new ArgumentNullException(nameof(options)), htmlQuery, dateTimeParser)
    {
    }

    public async Task<Dictionary<string, object?>> GetAsync(string utf8EncodedHtmlString, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8EncodedHtmlString);

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (!_htmlParsingSettings.Enabled || _htmlParsingSettings.Queries.Length == 0)
        {
            return result;
        }

        foreach (var query in _htmlParsingSettings.Queries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = query.Alias.Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (query.Selectors.Length == 0)
            {
                result[key] = null;
                continue;
            }

            var workingHtml = utf8EncodedHtmlString;
            if (query.RemoveSelectors.Length > 0)
            {
                var removed = await _htmlQuery.RemoveQueryAsync(
                    utf8EncodedHtmlString,
                    query.RemoveSelectors,
                    cancellationToken).ConfigureAwait(false);

                workingHtml = removed.Value;
            }

            var match = await _htmlQuery.QueryAsync(
                workingHtml,
                query.Selectors,
                query.ValueStrategy,
                query.AttributeName, 
                cancellationToken)
                .ConfigureAwait(false);

            var processed = ApplyInstructions(match.Value, query.ValueInstructions);
            result[key] = processed;
        }

        return result;
    }

    private object? ApplyInstructions(string? value, HtmlValueInstructions instructions)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var working = value;

        if (instructions.HasFlag(HtmlValueInstructions.RemoveAttributes))
        {
            working = RemoveAttributes(working);
        }

        if (instructions.HasFlag(HtmlValueInstructions.AbsoluteUrls))
        {
            working = AbsoluteUrls(working);
        }

        if (instructions.HasFlag(HtmlValueInstructions.Trim))
        {
            working = working.Trim();
        }

        if (instructions.HasFlag(HtmlValueInstructions.ParseDate))
        {
            if (_dateTimeParser.TryParse(working, out var parsedDate))
            {
                return parsedDate;
            }
        }

        return string.IsNullOrWhiteSpace(working) ? null : working;
    }

    private string AbsoluteUrls(string utf8EncodedHtmlString)
    {
        return _htmlQuery.MakeLinksAbsoluteAsync(utf8EncodedHtmlString, _htmlParsingSettings.BaseUrl).GetAwaiter().GetResult().Value;
    }

    
    private string RemoveAttributes(string utf8EncodedHtmlString)
    {
        return _htmlQuery.RemoveAttributesAsync(utf8EncodedHtmlString, null, _htmlParsingSettings.PreservedTags).GetAwaiter().GetResult().Value;
    }
}
