using System.Security.Cryptography;
using System.Text;

namespace HttpDancer.Naming.Hashing;

/// <summary>
/// Generates a short, YouTube-like hash using SHA256 and Base62 encoding.
/// </summary>
public class Base62HashGenerator : IHashGenerator
{
    private const string Base62Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public string GenerateHash(string value, int minLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var hashBytes = ComputeHashBytes(value);
        var encoded = EncodeBase62(hashBytes);

        return encoded.Length >= minLength 
            ? encoded[..minLength] 
            : encoded.PadRight(minLength, '0');
    }

    private static byte[] ComputeHashBytes(string value)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeBase62(byte[] bytes)
    {
        // Use the first 10 bytes (80 bits) for a compact yet collision-resistant value.
        const int byteCount = 10;
        var usableBytes = bytes.Length >= byteCount ? bytes[..byteCount] : bytes;

        var accumulator = new System.Numerics.BigInteger(usableBytes.Concat(new byte[] { 0 }).ToArray());
        var builder = new StringBuilder();

        while (accumulator > 0)
        {
            accumulator = System.Numerics.BigInteger.DivRem(accumulator, Base62Alphabet.Length, out var remainder);
            builder.Insert(0, Base62Alphabet[(int)remainder]);
        }

        return builder.Length == 0 ? Base62Alphabet[0].ToString() : builder.ToString();
    }
}
