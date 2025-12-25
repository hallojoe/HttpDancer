namespace HttpDancer.Naming.Hashing;

/// <summary>
/// Generates compact, URL-friendly hashes for query strings or other inputs.
/// </summary>
public interface IHashGenerator
{
    string GenerateHash(string value, int minLength);
}
