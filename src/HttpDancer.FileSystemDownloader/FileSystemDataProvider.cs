using System.Text;
using HttpDancer.FileSystemDownloader.Configuration;
using Microsoft.Extensions.Options;

namespace HttpDancer.FileSystemDownloader;

public class FileSystemDataProvider(IOptionsMonitor<FileSystemDownloaderSettings> fileSystemDataSettingsOptionsMonitor) : IFileSystemDataProvider
{
    private readonly string _baseDirectory = 
        fileSystemDataSettingsOptionsMonitor.CurrentValue.Workspace ?? throw new ArgumentNullException(nameof(FileSystemDownloaderSettings.Workspace));
    
    public string[] List(string searchPattern)
    {
        return string.IsNullOrWhiteSpace(_baseDirectory) 
            ? [] 
            : Directory.GetFiles(_baseDirectory, searchPattern, SearchOption.AllDirectories);
    }
    
    public async Task<string> ReadStringAsync(string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "";
        }

        var pathAndFilename = CreatePath(filename);
        
        return await File.ReadAllTextAsync(pathAndFilename, Encoding.UTF8, cancellationToken);
    }
    
    public async Task WriteStringAsync(string relativePathWithFilename, string content, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePathWithFilename) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        CreateDirectory(relativePathWithFilename);

        var pathAndFilename = CreateSafePath(relativePathWithFilename);

        var target = overwrite ? pathAndFilename : GetNextAvailableFileName(pathAndFilename);

        await File.WriteAllTextAsync(target, content, Encoding.UTF8, cancellationToken);
    }
    
    public async Task WriteBytesAsync(string relativePathWithFilename, byte[] content, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePathWithFilename) || content is {Length:0})
        {
            return;
        }

        CreateDirectory(relativePathWithFilename);

        var pathAndFilename = CreateSafePath(relativePathWithFilename);

        var target = overwrite ? pathAndFilename : GetNextAvailableFileName(pathAndFilename);

        await File.WriteAllBytesAsync(target, content, cancellationToken);
    }

    public void CreateDirectory(string pathAndOptionalFilename)
    {
        if (string.IsNullOrWhiteSpace(pathAndOptionalFilename))
        {
            return;
        }

        var directoryName = Path.GetDirectoryName(Path.Join(_baseDirectory, pathAndOptionalFilename));

        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return;
        }

        Directory.CreateDirectory(directoryName);
    }
    
    public async Task<string[]> ReadAllLinesAsync(string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_baseDirectory) || string.IsNullOrWhiteSpace(filename))
        {
            return [];
        }

        var targetPathAndFilename = CreatePath(filename);

        var contents = await File.ReadAllLinesAsync(targetPathAndFilename, Encoding.UTF8, cancellationToken);

        var result = contents
            .Where(line => string.IsNullOrWhiteSpace(line) is false)
            .Select(line => line.Trim())
            .Distinct()
            .ToArray();

        return result;
    }

    public async Task<string[]> ReadAllLinesAsync(string[] filenames, CancellationToken cancellationToken = default)
    {
        if (filenames.Length == 0)
        {
            return [];
        }

        var result = new List<string>();
        
        foreach (var filename in filenames)
        {
            var contents = await ReadAllLinesAsync(filename, cancellationToken);

            result.AddRange(contents);
        }
        
        return result.Distinct().ToArray();
    }
    
    public string EnsureMaxFileNameLength(string fileName, int maxLength = 255)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be null or empty.", nameof(fileName));
        }

        if (fileName.Length <= maxLength)
        {
            return fileName;
        }

        // Keep extension intact while truncating from the left
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Calculate how much of the name we can keep
        var maxNameLength = maxLength - extension.Length;
        if (maxNameLength <= 0)
        {
            throw new InvalidOperationException("Max length is too small to accommodate the extension.");
        }

        // Take last N characters of the name
        var truncatedName = nameWithoutExtension[^maxNameLength..];

        return truncatedName + extension;
    }
    
    private string CreatePath(string filename) => Path.Join(_baseDirectory, filename.Trim('/').Replace('/', '\\'));

    private string CreateSafePath(string relativePathWithFilename)
    {
        var path = CreatePath(relativePathWithFilename);
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var file = Path.GetFileName(path);
        var safeFile = EnsureMaxFileNameLength(file);
        return Path.Combine(directory, safeFile);
    }
    
    private string GetNextAvailableFileName(string filename)
    {
        if (!File.Exists(filename))
        {
            return filename;
        }

        var directory = Path.GetDirectoryName(filename) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(filename);
        var extension = Path.GetExtension(filename);

        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}({counter}){extension}");
            counter++;
            
        } while (File.Exists(candidate));

        return candidate;
    }
    
}
