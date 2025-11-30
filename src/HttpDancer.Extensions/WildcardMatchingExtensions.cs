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

        return input.AsSpan().IsMatch(pattern.AsSpan(), ignoreCase);
    }

    /// <summary>
    /// Wildcard match against multiple patterns (any match returns true).
    /// </summary>
    public static bool IsAnyMatch(string input, IEnumerable<string> patterns, bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(input);

        var inputSpan = input.AsSpan();
        foreach (var pattern in patterns)
        {
            if (inputSpan.IsMatch(pattern.AsSpan(), ignoreCase))
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
        var patternIndex = 0;
        var inputIndex = 0;

        // Index of the last '*' in the pattern, or -1 if none seen yet
        var lastStarPatternIndex = -1;

        // Input index we were at when we last saw a '*'
        var lastStarInputIndex = 0;

        // Walk the input
        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length)
            {
                var patternChar = pattern[patternIndex];

                // '*' can match zero or more characters
                if (patternChar == '*')
                {
                    // Remember where the '*' is and where we are in input
                    lastStarPatternIndex = patternIndex;
                    lastStarInputIndex = inputIndex;

                    // Move past '*'
                    patternIndex++;
                    continue;
                }

                var inputChar = input[inputIndex];

                // Direct match or '?' single-character wildcard
                if (patternChar == '?' || CharsEqual(patternChar, inputChar, ignoreCase))
                {
                    patternIndex++;
                    inputIndex++;
                    continue;
                }
            }

            // If we had a previous '*', backtrack: let '*' absorb one more character
            if (lastStarPatternIndex != -1)
            {
                // Reset pattern position to the first char after '*'
                patternIndex = lastStarPatternIndex + 1;

                // Extend the match of '*' by one character
                lastStarInputIndex++;
                inputIndex = lastStarInputIndex;
                continue;
            }

            // No match and no '*' to fall back on
            return false;
        }

        // Consume any trailing '*' in the pattern
        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        // Match only if we've consumed the entire pattern
        return patternIndex == pattern.Length;
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

        return input.AsSpan().IsAnyMatch(patterns.AsSpan(), ignoreCase, separator);
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