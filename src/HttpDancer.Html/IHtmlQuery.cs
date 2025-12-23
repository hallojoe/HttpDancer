namespace HttpDancer.Html;

public interface IHtmlQuery
{
    /// <summary>
    /// Select the first element that matches any selector (evaluated in order).
    /// </summary>
    /// <param name="utf8EncodeHtmlString">A UTF-8 encoded HTML document or fragment.</param>
    /// <param name="selectors">Ordered CSS selectors to try; stops at the first selector that yields a match.</param>
    /// <param name="attributeName"></param>
    /// <param name="valueStrategy"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString> QueryAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Select the first element that matches any selector (evaluated in order) from the provided HTML wrapper.
    /// </summary>
    /// <param name="utf8EncodeHtmlString"></param>
    /// <param name="selectors"></param>
    /// <param name="attributeName"></param>
    /// <param name="valueStrategy"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString> QueryAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Select many elements from the HTML string using the first selector that yields any matches.
    /// </summary>
    /// <param name="utf8EncodeHtmlString">Can be a full HTML document or an HTML fragment.</param>
    /// <param name="selectors">Ordered CSS selectors to try; stops at the first selector that yields one or more matches.</param>
    /// <param name="attributeName"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="valueStrategy"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString[]> QueryAllAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Select many elements from the HTML wrapper using the first selector that yields any matches.
    /// </summary>
    /// <param name="utf8EncodeHtmlString"></param>
    /// <param name="selectors"></param>
    /// <param name="attributeName"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="valueStrategy"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString[]> QueryAllAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Remove all elements from the HTML string that match any of the specified selectors.
    /// </summary>
    /// <param name="utf8EncodeHtmlString"></param>
    /// <param name="selectors"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString> RemoveQueryAsync(string utf8EncodeHtmlString, string[] selectors,
        CancellationToken cancellationToken);

    /// <summary>
    /// Return minified outer HTML for all elements matched by the first selector that yields any results.
    /// </summary>
    /// <param name="utf8EncodeHtmlString"></param>
    /// <param name="selectors"></param>
    /// <param name="attributeName"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="valueStrategy"></param>
    /// <returns></returns>
    Task<IUtf8EncodedHtmlString[]> MinifyQueryAsync(
        string utf8EncodeHtmlString,
        string[] selectors,
        HtmlValueStrategy valueStrategy = HtmlValueStrategy.OuterHtml,
        string attributeName = "content",
        CancellationToken cancellationToken = default
    );

    Task<IUtf8EncodedHtmlString> RemoveAttributesAsync(
        string utf8EncodeHtmlString,
        string[]? attributeNames = null,
        string[]? preservedTagNames = null,
        CancellationToken cancellationToken = default
    );
    
    
    Task<IUtf8EncodedHtmlString> MakeLinksAbsoluteAsync(
        string utf8EncodeHtmlString,
        string baseUrl,
        CancellationToken cancellationToken = default
    );

    Task<IUtf8EncodedHtmlString> RemoveAttributesAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string[]? attributeNames = null,
        string[]? preservedTagNames = null,
        CancellationToken cancellationToken = default
    );
    
    
    Task<IUtf8EncodedHtmlString> MakeLinksAbsoluteAsync(
        IUtf8EncodedHtmlString utf8EncodeHtmlString,
        string baseUrl,
        CancellationToken cancellationToken = default
    );

}
