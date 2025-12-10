namespace HttpDancer.FileSystemDownloader;

public interface IFileSystemDataProvider
{
    string[] List(string searchPattern);
    void CreateDirectory(string pathAndOptionalFilename);
    Task<string> ReadStringAsync(string filename, CancellationToken cancellationToken = default);
    Task WriteStringAsync(string relativePathWithFilename, string content, bool overwrite = true, CancellationToken cancellationToken = default);
    Task WriteBytesAsync(string relativePathWithFilename, byte[] content, bool overwrite = true, CancellationToken cancellationToken = default);
    Task<string[]> ReadAllLinesAsync(string filename, CancellationToken cancellationToken = default);
    Task<string[]> ReadAllLinesAsync(string[] filenames, CancellationToken cancellationToken = default);
}