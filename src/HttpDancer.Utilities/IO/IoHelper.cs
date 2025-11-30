using System.Net;
using System.Text;

namespace HttpDancer.Utilities.IO;

public static class IoHelper
{
    public static string[] GetFiles(string baseDirectory, string searchPattern)
    {
        return string.IsNullOrWhiteSpace(baseDirectory) 
            ? [] 
            : Directory.GetFiles(baseDirectory, searchPattern, SearchOption.AllDirectories);
    }
    
    public static async Task<string> ReadString(string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filename)) return "";

        return await File.ReadAllTextAsync(filename, Encoding.UTF8, cancellationToken);
    }

    public static async Task WriteStringAsync(string filename, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(content)) return;

        await File.WriteAllTextAsync(filename, content, Encoding.UTF8, cancellationToken);
    }

    public static async Task<string[]> ReadAllLinesAsync(string baseDirectory, string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(filename)) return [];

        var targetPath = Path.Combine(baseDirectory, filename);

        var contents = await File.ReadAllLinesAsync(targetPath, Encoding.UTF8, cancellationToken);

        var result = contents
            .Where(line => string.IsNullOrWhiteSpace(line) is false)
            .Select(line => line.Trim())
            .Distinct()
            .ToArray();

        return result;
    }

    public static async Task<string[]> ReadAllLinesAsync(string baseDirectory, string[] filenames, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || filenames.Length == 0) return [];

        var result = new List<string>();
        
        foreach (var filename in filenames)
        {
            var contents = await ReadAllLinesAsync(baseDirectory, filename, cancellationToken);

            result.AddRange(contents);
        }
        
        return result.Distinct().ToArray();
    }
    
    public static string EnsureMaxFileNameLength(string fileName, int maxLength = 255)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name must not be null or empty.", nameof(fileName));

        if (fileName.Length <= maxLength)
            return fileName;

        // Keep extension intact while truncating from the left
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Calculate how much of the name we can keep
        var maxNameLength = maxLength - extension.Length;
        if (maxNameLength <= 0)
            throw new InvalidOperationException("Max length is too small to accommodate the extension.");

        // Take last N characters of the name
        var truncatedName = nameWithoutExtension[^maxNameLength..];

        return truncatedName + extension;
    }
}
