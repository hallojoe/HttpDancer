using System.Globalization;

namespace HttpDancer.Core.Parsing;

public interface IDateTimeParser
{
    /// <summary>
    /// Attempts to get a date even when the string contains other text.
    /// </summary>
    DateTime? Get(
        string? value,
        out DateTimeOffset result,
        string[]? customFormats = null,
        string? locale = null);

    /// <summary>
    /// Attempts to parse a date even when the string contains other text.
    /// </summary>
    bool TryParse(
        string? input,
        out DateTimeOffset result,
        string[]? customFormats = null,
        string? locale = null);
}

public class DateTimeParser : IDateTimeParser
{
    private static readonly string[] DefaultFormats =
    {
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.fffK",
        "yyyy-MM-ddTHH:mm:ss.fffffffK",
        "r",
        "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
        "MMMM d, yyyy",
        "MMM d, yyyy",
        "MMMM d, yyyy HH:mm",
        "MMM d, yyyy HH:mm",
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "dd-MM-yyyy"
    };

    /// <summary>
    /// Attempts to get a date even when the string contains other text.
    /// </summary>
    public DateTime? Get(
        string? value,
        out DateTimeOffset result,
        string[]? customFormats = null,
        string? locale = null)
    {
        if (!TryParse(value, out result, customFormats, locale))
        {
            return null;
        }
        return result.UtcDateTime;
    }
    
    /// <summary>
    /// Attempts to parse a date even when the string contains other text.
    /// </summary>
    public bool TryParse(
        string? input,
        out DateTimeOffset result,
        string[]? customFormats = null,
        string? locale = null)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var provider = locale != null
            ? new CultureInfo(locale)
            : CultureInfo.InvariantCulture;

        var styles =
            DateTimeStyles.AllowWhiteSpaces |
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal;

        // Split on whitespace and punctuation groups
        var tokens = input.Split(
            [' ', '\t', '\r', '\n', '•', '|', ',', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries);
        
        // 1) Try user formats first
        if (customFormats is { Length: > 0 })
        {
            foreach (var token in tokens)
            {
                if (DateTimeOffset.TryParseExact(token, customFormats, provider, styles, out result))
                    return true;
            }
        }

        // 2) Try built-in common formats
        foreach (var token in tokens)
        {
            if (DateTimeOffset.TryParseExact(token, DefaultFormats, provider, styles, out result))
                return true;
        }

        // 3) Try flexible fallback parser on full string
        if (DateTimeOffset.TryParse(input, provider, styles, out result))
            return true;

        // 4) Try fallback parser on each token
        foreach (var token in tokens)
        {
            if (DateTimeOffset.TryParse(token, provider, styles, out result))
                return true;
        }

        return false;
    }
}
