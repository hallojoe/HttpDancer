namespace HttpDancer.FileSystemDownloader.Minification;

public interface IHtmlMinifier
{
    string Minify(string utf8EncodedHtmlString, IEnumerable<string>? selectorsToRemove = null);
}
