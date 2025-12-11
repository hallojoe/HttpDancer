namespace HttpDancer.Html;

// TODO: make IHtmlQuery implementation
public interface IHtmlQuery
{
    //TODO: improve summary text
    /// <summary>
    /// Select a single element from the HTML string.
    /// </summary>
    /// <param name="utf8EncodeHtmlString">Can be a full HTML document or an HTML fragment.</param>
    /// <param name="selectors">The queries to used to get a single value.
    /// If many selectors are present then the logic is:
    ///  - Try to find a value using first
    ///  - If no value is found then try the second, etc.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString> QueryAsync(string utf8EncodeHtmlString, string[] selectors, CancellationToken cancellationToken);

    //TODO: improve summary text
    /// <summary>
    /// Select many elements from the HTML string.
    /// </summary>
    /// <param name="utf8EncodeHtmlString">Can be a full HTML document or an HTML fragment.</param>
    /// <param name="selectors">The queries to used to get many values.
    /// If many selectors are present then the logic is:
    ///  - Try to find many values using first
    ///  - If no values are found then try the second, etc.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString[]> QueryAllAsync(string utf8EncodeHtmlString, string selectors, CancellationToken cancellationToken);

    //TODO: make summary text
    /// <summary>
    /// 
    /// </summary>
    /// <param name="utf8EncodeHtmlString"></param>
    /// <param name="selectors"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString[]> RemoveQueryAsync(string utf8EncodeHtmlString, string[] selectors, CancellationToken cancellationToken);
}