namespace HttpDancer.Parsing;

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