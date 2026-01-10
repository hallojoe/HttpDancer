namespace HttpDancer.Parsing.LinkHttpHeaderParser;

/// <summary>
/// Parser for RFC 8288-ish Link headers (practical, robust parser).
/// Supports:
/// - Multiple Link header lines
/// - Multiple comma-separated links per line
/// - Parameters: token or quoted-string values, and valueless parameters (rare, but allowed)
/// - Whitespace variations
/// - Percent-encoded hrefs (kept as-is; not decoded)
/// 
/// Not supported (by design; uncommon in real traffic):
/// - Comma inside unquoted parameter values (invalid anyway)
/// </summary>
public sealed class DefaultLinkHttpHttpHeaderParser : ILinkHttpHeaderParser
{
    /// <summary>
    /// Parse all "Link" headers in <paramref name="linkHeaderValues"/> into a flat list of LinkHttpHeader entries.
    /// </summary>
    public IReadOnlyList<LinkHttpHeader> Parse(IReadOnlyList<string> linkHeaderValues)
    {
        ArgumentNullException.ThrowIfNull(linkHeaderValues);

        var result = new List<LinkHttpHeader>();

        foreach (var linkHeaderValue in linkHeaderValues)
        {
            if (string.IsNullOrWhiteSpace(linkHeaderValue))
            {
                continue;
            }

            ParseLinkHeaderValue(linkHeaderValue, result);
        }

        return result.AsReadOnly();
    }
    
    private static string ToAbsoluteHref(string baseUrl, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        // Uri treats leading '//' as scheme-relative; handle it as absolute based on base scheme.
        if (href.StartsWith("//", StringComparison.Ordinal))
        {
            return new Uri($"{new Uri(baseUrl, UriKind.Absolute).Scheme}:{href}", UriKind.Absolute).ToString();
        }

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(baseUri, href).ToString();
    }
    
    private static string DecodeUrlComponent(string s) => string.IsNullOrEmpty(s) ? s : Uri.UnescapeDataString(s);

    private static void ParseLinkHeaderValue(string value, List<LinkHttpHeader> output)
    {
        var i = 0;
        while (true)
        {
            SkipOws(value, ref i);
            if (i >= value.Length)
            {
                return;
            }

            // Each link-value starts with <...>
            if (value[i] != '<')
            {
                // Be forgiving: try to skip to next comma and continue.
                SkipToNextComma(value, ref i);
                if (!ConsumeComma(value, ref i))
                {
                    return;
                }

                continue;
            }

            i++; // consume '<'
            var href = ReadUntil(value, ref i, '>');
            href = DecodeUrlComponent(href);

            if (i >= value.Length || value[i] != '>')
            {
                // Malformed; abort this item and try next.
                SkipToNextComma(value, ref i);
                if (!ConsumeComma(value, ref i))
                {
                    return;
                }

                continue;
            }
            i++; // consume '>'

            var attributes = new List<NamedString>();

            // Parameters: *( OWS ; OWS param )
            while (true)
            {
                SkipOws(value, ref i);
                if (i >= value.Length)
                {
                    break;
                }

                if (value[i] == ',')
                {
                    // End of this link-value
                    break;
                }

                if (value[i] != ';')
                {
                    // Unexpected token; try to recover by skipping to comma/end.
                    SkipToNextComma(value, ref i);
                    break;
                }

                i++; // consume ';'
                SkipOws(value, ref i);

                // param-name
                var name = ReadToken(value, ref i);
                if (name.Length == 0)
                {
                    // Malformed param; recover
                    SkipToNextComma(value, ref i);
                    break;
                }

                SkipOws(value, ref i);

                string paramValue;

                if (i < value.Length && value[i] == '=')
                {
                    i++; // consume '='
                    SkipOws(value, ref i);

                    if (i < value.Length && value[i] == '"')
                    {
                        paramValue = ReadQuotedString(value, ref i);
                    }
                    else
                    {
                        // token value until delimiter (OWS, ';', ',' or end)
                        paramValue = ReadToken(value, ref i);
                    }
                }
                else
                {
                    // valueless parameter (rare but legal); represent as empty string
                    paramValue = string.Empty;
                }

                attributes.Add(new NamedString(name, DecodeUrlComponent(paramValue)));
            }
            
            output.Add(new LinkHttpHeader(href, attributes.AsReadOnly()));

            SkipOws(value, ref i);

            // Consume optional comma to proceed to next link-value
            if (!ConsumeComma(value, ref i))
            {
                // no more items
                return;
            }
        }
    }

    /// <summary>
    /// Read until <paramref name="terminator"/> or end. Returns the read substring.
    /// Leaves index positioned at terminator or end.
    /// </summary>
    private static string ReadUntil(string s, ref int i, char terminator)
    {
        var start = i;
        while (i < s.Length && s[i] != terminator)
        {
            i++;
        }
        return s.Substring(start, i - start).Trim();
    }

    /// <summary>
    /// Reads an RFC token (loosely): 1*(ALPHA / DIGIT / "!" / "#" / "$" / "%" / "&" / "'" / "*" / "+" / "-" / "." / "^" / "_" / "`" / "|" / "~")
    /// Here we use a pragmatic subset: stop at whitespace, ';', ',', '='.
    /// </summary>
    private static string ReadToken(string s, ref int i)
    {
        var start = i;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c) || c == ';' || c == ',' || c == '=')
            {
                break;
            }

            // Also stop at DQUOTE (should be handled by quoted-string reader)
            if (c == '"')
            {
                break;
            }

            i++;
        }

        if (i == start)
        {
            return string.Empty;
        }

        return s.Substring(start, i - start);
    }

    /// <summary>
    /// Reads a quoted-string starting at current index which must be '"'.
    /// Supports backslash escaping \" and \\ (and generally \" for any char).
    /// Leaves index positioned after the closing quote, or at end if malformed.
    /// </summary>
    private static string ReadQuotedString(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"')
        {
            return string.Empty;
        }

        i++; // consume opening quote
        var buf = new System.Text.StringBuilder();

        while (i < s.Length)
        {
            var c = s[i];

            switch (c)
            {
                case '"':
                    i++; // consume closing quote
                    return buf.ToString();
                case '\\' when i + 1 < s.Length:
                    // quoted-pair
                    i++;
                    buf.Append(s[i]);
                    i++;
                    continue;
                default:
                    buf.Append(c);
                    i++;
                    break;
            }
        }

        // malformed: no closing quote
        return buf.ToString();
    }

    private static void SkipOws(string s, ref int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
            i++;
    }

    private static void SkipToNextComma(string s, ref int i)
    {
        var inQuotes = false;

        while (i < s.Length)
        {
            var c = s[i];

            if (c == '"' && !IsEscaped(s, i))
            {
                inQuotes = !inQuotes;
            }

            if (!inQuotes && c == ',')
            {
                return;
            }

            i++;
        }
    }

    private static bool ConsumeComma(string s, ref int i)
    {
        SkipOws(s, ref i);
        if (i >= s.Length || s[i] != ',')
        {
            return false;
        }

        i++; // consume comma
        return true;
    }

    private static bool IsEscaped(string s, int quoteIndex)
    {
        // Count backslashes immediately preceding the quote.
        var slashCount = 0;
        var j = quoteIndex - 1;
        while (j >= 0 && s[j] == '\\')
        {
            slashCount++;
            j--;
        }
        return (slashCount % 2) == 1;
    }
}