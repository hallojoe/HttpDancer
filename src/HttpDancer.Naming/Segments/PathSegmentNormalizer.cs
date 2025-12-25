using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HttpDancer.Naming.Enums;
using HttpDancer.Naming.Slug;

namespace HttpDancer.Naming.Segments;

/// <summary>
/// Applies decoding, casing, slug and diacritic strategies to individual segments.
/// </summary>
public partial class PathSegmentNormalizer(ISlugify slugify) : IPathSegmentNormalizer
{
    public string NormalizeSegment(string segment, UrlNamingOptions options)
    {
        var normalized = ApplyDecoding(segment, options.DecodingStrategy);
        normalized = ApplyNormalizationSteps(normalized, options.NormalizationSteps);
        normalized = ApplySlugStrategy(normalized, options.SlugStrategy, options.ReplacementChar);
        normalized = ApplyCasing(normalized, options.CasingStrategy);
        normalized = TrimReplacementCharacters(normalized, options.ReplacementChar);
        return normalized;
    }

    public string NormalizeToken(string token, UrlNamingOptions options)
    {
        return NormalizeSegment(token, options);
    }

    private static string ApplyDecoding(string value, DecodingStrategy decodingStrategy)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return decodingStrategy switch
        {
            DecodingStrategy.DecodeAndNormalize => WebUtility.UrlDecode(value).Normalize(NormalizationForm.FormC),
            DecodingStrategy.DecodeOnlyAscii => DecodeAsciiOnly(value),
            DecodingStrategy.KeepEncoded => value,
            _ => value
        };
    }

    private static string DecodeAsciiOnly(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '%' && index + 2 < value.Length)
            {
                var hexValue = value.Substring(index + 1, 2);
                if (byte.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var byteValue))
                {
                    builder.Append(byteValue <= sbyte.MaxValue ? (char)byteValue : $"%{hexValue}");
                    index += 2;
                    continue;
                }
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    private string ApplyNormalizationSteps(string value, NormalizationSteps normalizationSteps)
    {
        var result = value;

        if (normalizationSteps.HasFlag(NormalizationSteps.Trim))
        {
            result = result.Trim(' ', '\t', '\r', '\n', '/', '\\');
        }

        if (normalizationSteps.HasFlag(NormalizationSteps.UnicodeNormalize))
        {
            result = result.Normalize(NormalizationForm.FormC);
        }

        if (normalizationSteps.HasFlag(NormalizationSteps.StripDiacritics))
        {
            result = RemoveDiacritics(result);
        }

        if (normalizationSteps.HasFlag(NormalizationSteps.ToLower))
        {
            result = result.ToLowerInvariant();
        }

        if (normalizationSteps.HasFlag(NormalizationSteps.Slugify))
        {
            result = ApplySlugStrategy(result, SlugStrategy.StrictSlug, '-');
        }

        return result;
    }

    private string ApplySlugStrategy(string value, SlugStrategy slugStrategy, char replacementChar)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return slugStrategy switch
        {
            SlugStrategy.None => value,
            SlugStrategy.ReplaceSpacesWithDash => ReplaceSpaces(value, replacementChar),
            SlugStrategy.StrictSlug => StrictSlugify(value, replacementChar),
            _ => value
        };
    }

    private static string ReplaceSpaces(string value, char replacementChar)
    {
        var replaced = Regex.Replace(value, "\\s+", replacementChar.ToString(CultureInfo.InvariantCulture));
        return CollapseRepeatedReplacement(replaced, replacementChar);
    }

    private string StrictSlugify(string value, char replacementChar)
    {
        var slugValue = slugify.CreateSlug(value);
        return slugValue.Replace('-', replacementChar);
        
        // var lowered = value.ToLowerInvariant();
        // var sanitized = StrictSlugExpression().Replace(lowered, replacementChar.ToString(CultureInfo.InvariantCulture));
        // return CollapseRepeatedReplacement(sanitized, replacementChar);
    }

    private static string CollapseRepeatedReplacement(string value, char replacementChar)
    {
        var pattern = $"{Regex.Escape(replacementChar.ToString(CultureInfo.InvariantCulture))}{{2,}}";
        var collapsed = Regex.Replace(value, pattern, replacementChar.ToString(CultureInfo.InvariantCulture));
        return collapsed.Trim(replacementChar);
    }

    private static string ApplyCasing(string value, CasingStrategy casingStrategy)
    {
        return casingStrategy switch
        {
            CasingStrategy.Preserve => value,
            CasingStrategy.Lowercase => value.ToLower(CultureInfo.CurrentCulture),
            CasingStrategy.LowercaseInvariant => value.ToLowerInvariant(),
            _ => value
        };
    }

    private static string TrimReplacementCharacters(string value, char replacementChar)
    {
        return value.Trim(replacementChar);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex StrictSlugExpression();
}
