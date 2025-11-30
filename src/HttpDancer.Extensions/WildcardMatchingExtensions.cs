namespace HttpDancer.Extensions;

public static class WildcardMatchingExtensions
{
    /// <summary>
    /// Wildcard match using strings. Supports '*' and '?'.
    /// Delegates to the Span-based implementation.
    /// </summary>
    public static bool IsMatch(this string input, string pattern, bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(input);

        // Convenience: allow comma-separated patterns via IsAnyMatch.
        if (pattern.IndexOf(',') >= 0)
        {
            return input.IsAnyMatch(pattern, ignoreCase);
        }

        // Host-friendly: if the pattern looks like a hostname/glob and the input is a URI,
        // match against the host instead of the full URL.
        var patternLooksLikeHost = pattern.IndexOf('/') == -1 && pattern.IndexOf(':') == -1 && pattern.IndexOf('.') >= 0;

        if (patternLooksLikeHost &&
            Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            string.IsNullOrEmpty(uri.Host) is false)
        {
            return uri.Host.AsSpan().IsMatch(pattern.AsSpan(), ignoreCase);
        }

        return input.AsSpan().IsMatch(pattern.AsSpan(), ignoreCase);
    }

    /// <summary>
    /// Wildcard match against multiple patterns (any match returns true).
    /// </summary>
    public static bool IsAnyMatch(string input, IEnumerable<string> patterns, bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(input);

        foreach (var pattern in patterns)
        {
            if (input.IsMatch(pattern, ignoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Wildcard match using ReadOnlySpan&lt;char&gt; (no allocations).
    /// Supports '*' (multi-char) and '?' (single-char).
    /// </summary>
    public static bool IsMatch(this ReadOnlySpan<char> input, ReadOnlySpan<char> pattern, bool ignoreCase = true)
    {
        var i = 0;
        var j = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (i < input.Length)
        {
            if (j < pattern.Length && (pattern[j] == '?' || CharsEqual(pattern[j], input[i], ignoreCase)))
            {
                i++;
                j++;
                continue;
            }

            if (j < pattern.Length && pattern[j] == '*')
            {
                starIndex = j;
                matchIndex = i;
                j++;
                continue;
            }

            if (starIndex != -1)
            {
                j = starIndex + 1;
                matchIndex++;
                i = matchIndex;
                continue;
            }

            return false;
        }

        while (j < pattern.Length && pattern[j] == '*')
        {
            j++;
        }

        return j == pattern.Length;
    }

    /// <summary>
    /// Matches input against a comma-separated list of patterns (Span-based).
    /// Example: patterns = "text/*,image/*"
    /// </summary>
    public static bool IsAnyMatch(this ReadOnlySpan<char> input, ReadOnlySpan<char> patterns, bool ignoreCase = true, char separator = ',')
    {
        var start = 0;

        while (start <= patterns.Length)
        {
            var relativeIndex = patterns.Slice(start).IndexOf(separator);
            ReadOnlySpan<char> pattern;

            if (relativeIndex == -1)
            {
                // Last (or only) segment
                pattern = patterns[start..].Trim();
                if (!pattern.IsEmpty && input.IsMatch(pattern, ignoreCase))
                {
                    return true;
                }

                break;
            }

            // Segment up to the separator
            pattern = patterns.Slice(start, relativeIndex).Trim();
            if (!pattern.IsEmpty && input.IsMatch(pattern, ignoreCase))
            {
                return true;
            }

            // Skip past separator for the next iteration
            start += relativeIndex + 1;
        }

        return false;
    }

    /// <summary>
    /// Overload for comma-separated patterns in a string.
    /// Example: IsAnyMatch("text/*,image/*", "image/jpeg")
    /// </summary>
    public static bool IsAnyMatch(this string input, string patterns, bool ignoreCase = true, char separator = ',')
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(input);

        foreach (var raw in patterns.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var pattern = raw.Trim();
            if (pattern.Length == 0)
            {
                continue;
            }

            if (input.IsMatch(pattern, ignoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CharsEqual(char a, char b, bool ignoreCase)
    {
        if (!ignoreCase)
        {
            return a == b;
        }

        if (a == b)
        {
            return true;
        }
        // Case-insensitive comparison without allocations
        return char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
    }
}
