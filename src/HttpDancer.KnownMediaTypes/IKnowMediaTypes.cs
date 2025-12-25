namespace HttpDancer.KnownMediaTypes;

public interface IKnowMediaTypes
{
    /// <summary>
    /// Returns the mime type associated with the extension (with or without dot).
    /// If unknown, returns "application/octet-stream".
    /// </summary>
    string? GetMediaType(string? extension);

    /// <summary>
    /// Returns the file extension (without dot) associated with the MIME type.
    /// If unknown, returns "bin".
    /// </summary>
    string GetExtension(string? mimeType);
}