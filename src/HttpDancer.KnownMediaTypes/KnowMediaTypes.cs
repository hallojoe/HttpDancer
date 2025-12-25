using Microsoft.Extensions.Options;

namespace HttpDancer.KnownMediaTypes;


public class MediaTypes(IOptions<KnownMediaTypes> mimeTypeOptions) : IKnowMediaTypes
{
    public string? GetMediaType(string? extension)
    {
        extension = extension?.Trim('.');

        return string.IsNullOrWhiteSpace(extension) 
            ? null 
            : mimeTypeOptions.Value.Map.FirstOrDefault(pair => pair.Value.Equals(extension, StringComparison.OrdinalIgnoreCase)).Key;
    }

    /// <summary>
    /// Returns the file extension (without dot) associated with the MIME type.
    /// If unknown, returns "bin".
    /// </summary>
    public string GetExtension(string? mimeType)
    {
        return string.IsNullOrWhiteSpace(mimeType) 
            ? "bin" 
            : mimeTypeOptions.Value.Map.GetValueOrDefault(mimeType.Trim(), "bin");
    }
}