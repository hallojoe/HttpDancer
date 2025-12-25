using System.Text;

namespace HttpDancer.Naming.Slug;

public interface ISlugify
{
    string CreateSlug(string value);
}

public class Slugify : ISlugify
{
    public string CreateSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        const int maxLength = 80;
        var length = value.Length;
        var lastWasSeparator = false;
        var stringBuilder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            var currentCharacter = value[i];

            if (currentCharacter is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                stringBuilder.Append(currentCharacter);
                lastWasSeparator = false;
            }
            else if (currentCharacter is >= 'A' and <= 'Z')
            {
                // tricky way to convert to lowercase
                stringBuilder.Append((char)(currentCharacter | 32));
                lastWasSeparator = false;
            }
            else if (currentCharacter is ' ' or ',' or '.' or '/' or '\\' or '-' or '_' or '=')
            {
                if (!lastWasSeparator && stringBuilder.Length > 0)
                {
                    stringBuilder.Append('-');
                    lastWasSeparator = true;
                }
            }
            else if (currentCharacter >= 128)
            {
                var previousLength = stringBuilder.Length;
                stringBuilder.Append(RemapInternationalCharToAscii(currentCharacter));
                if (previousLength != stringBuilder.Length) lastWasSeparator = false;
            }
            if (i == maxLength) break;
        }

        return lastWasSeparator 
            ? stringBuilder.ToString()[..(stringBuilder.Length - 1)] 
            : stringBuilder.ToString();
    }
    
    private static string RemapInternationalCharToAscii(char c)
    {
        var s = c.ToString().ToLowerInvariant();

        if (s == "æ")
        {
            return "ae";
        }

        if (s == "ø")
        {
            return "oe";
        }

        if (s == "å")
        {
            return "aa";
        }

        if ("àåáâäãåą".Contains(s))
        {
            return "a";
        }

        if ("èéêëę".Contains(s))
        {
            return "e";
        }

        if ("ìíîïı".Contains(s))
        {
            return "i";
        }

        if ("òóôõöøőð".Contains(s))
        {
            return "o";
        }

        if ("ùúûüŭů".Contains(s))
        {
            return "u";
        }

        if ("çćčĉ".Contains(s))
        {
            return "c";
        }

        if ("żźž".Contains(s))
        {
            return "z";
        }

        if ("śşšŝ".Contains(s))
        {
            return "s";
        }

        if ("ñń".Contains(s))
        {
            return "n";
        }

        if ("ýÿ".Contains(s))
        {
            return "y";
        }

        if ("ğĝ".Contains(s))
        {
            return "g";
        }

        if (c == 'ř')
        {
            return "r";
        }

        if (c == 'ł')
        {
            return "l";
        }

        if (c == 'đ')
        {
            return "d";
        }

        if (c == 'ß')
        {
            return "ss";
        }

        if (c == 'Þ')
        {
            return "th";
        }

        if (c == 'ĥ')
        {
            return "h";
        }

        if (c == 'ĵ')
        {
            return "j";
        }

        return "";
    }
    
}