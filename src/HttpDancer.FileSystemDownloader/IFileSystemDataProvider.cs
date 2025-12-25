namespace HttpDancer.FileSystemDownloader;

public interface IFileSystemDataProvider
{
    string[] List(string searchPattern);
    void CreateDirectory(string pathAndOptionalFilename);
    Task<string> ReadStringAsync(string filename, CancellationToken cancellationToken = default);
    Task<string[]> ReadStringCollectionAsync(string[] filenames, CancellationToken cancellationToken = default);
    Task WriteStringAsync(string relativePathWithFilename, string content, bool overwrite = true, CancellationToken cancellationToken = default);
    Task WriteBytesAsync(string relativePathWithFilename, byte[] content, bool overwrite = true, CancellationToken cancellationToken = default);
    Task<string[]> ReadAllLinesAsync(string filename, CancellationToken cancellationToken = default);
    Task<string[]> ReadAllLinesAsync(string[] filenames, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for things matching the specified pattern and returns their contents as a dictionary. Where key is some identifier and value is the string content.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="searchPattern"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<string, string>> ReadStringsAsync(string path, string searchPattern = "*", CancellationToken cancellationToken = default);
}