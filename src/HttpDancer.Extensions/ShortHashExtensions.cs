using System.Security.Cryptography;
using System.Text;

namespace HttpDancer.Extensions;

public static class ShortHashExtensions
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string ToShortHash(this string input, int bytes = 8)
    {
        // Hash input using SHA256 (built-in and safe)
        var fullHash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Slice the hash to your desired number of bytes
        var slice = fullHash.AsSpan(0, bytes);

        // Encode slice to Base32 (built-in alphabet)
        return Base32Encode(slice).ToLowerInvariant();
    }

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        // Base32 produces 5 bits per character
        var outputLength = (int)Math.Ceiling(data.Length / 5d * 8);
        var result = new char[outputLength];

        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result[index++] = Base32Alphabet[(buffer >> bitsLeft) & 0x1F];
            }
        }

        if (bitsLeft > 0)
        {
            buffer <<= (5 - bitsLeft);
            result[index++] = Base32Alphabet[buffer & 0x1F];
        }

        return new string(result, 0, index);
    }
}
