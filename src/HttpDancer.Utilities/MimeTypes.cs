namespace HttpDancer.Utilities;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // HTML / Text
        ["text/html"] = "html",
        ["text/plain"] = "txt",
        ["text/css"] = "css",
        ["text/csv"] = "csv",
        ["text/javascript"] = "js",
        ["application/javascript"] = "js",
        ["application/json"] = "json",
        ["application/xml"] = "xml",
        ["text/xml"] = "xml",

        // Images
        ["image/jpeg"] = "jpg",
        ["image/jpg"] = "jpg",
        ["image/png"] = "png",
        ["image/gif"] = "gif",
        ["image/webp"] = "webp",
        ["image/bmp"] = "bmp",
        ["image/svg+xml"] = "svg",
        ["image/tiff"] = "tiff",
        ["image/x-icon"] = "ico",
        ["image/vnd.microsoft.icon"] = "ico",

        // Audio
        ["audio/mpeg"] = "mp3",
        ["audio/wav"] = "wav",
        ["audio/x-wav"] = "wav",
        ["audio/ogg"] = "ogg",
        ["audio/flac"] = "flac",
        ["audio/aac"] = "aac",
        ["audio/webm"] = "webm",

        // Video
        ["video/mp4"] = "mp4",
        ["video/webm"] = "webm",
        ["video/ogg"] = "ogv",
        ["video/x-msvideo"] = "avi",
        ["video/quicktime"] = "mov",
        ["video/mpeg"] = "mpeg",

        // Fonts
        ["font/ttf"] = "ttf",
        ["font/otf"] = "otf",
        ["font/woff"] = "woff",
        ["font/woff2"] = "woff2",
        ["application/font-woff"] = "woff",

        // Documents
        ["application/pdf"] = "pdf",
        ["application/msword"] = "doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = "docx",
        ["application/vnd.ms-excel"] = "xls",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = "xlsx",
        ["application/vnd.ms-powerpoint"] = "ppt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = "pptx",

        // Archives
        ["application/zip"] = "zip",
        ["application/x-zip-compressed"] = "zip",
        ["application/gzip"] = "gz",
        ["application/x-gzip"] = "gz",
        ["application/x-tar"] = "tar",
        ["application/x-7z-compressed"] = "7z",
        ["application/x-rar-compressed"] = "rar",

        // Binary streams / generic
        ["application/octet-stream"] = "bin",

        // Other
        ["application/xhtml+xml"] = "xhtml",
    };

    public static string? GetMimeType(string? extension)
    {
        extension = extension?.Trim('.');

        return string.IsNullOrWhiteSpace(extension) 
            ? null 
            : Map.FirstOrDefault(pair => pair.Value.Equals(extension, StringComparison.OrdinalIgnoreCase)).Key;
    }

    /// <summary>
    /// Returns the file extension (without dot) associated with the MIME type.
    /// If unknown, returns "bin".
    /// </summary>
    public static string GetExtension(string? mimeType)
    {
        return string.IsNullOrWhiteSpace(mimeType) 
            ? "bin" 
            : Map.GetValueOrDefault(mimeType.Trim(), "bin");
    }
    
}
