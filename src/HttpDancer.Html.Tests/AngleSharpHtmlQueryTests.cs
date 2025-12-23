// namespace HttpDancer.Html.Tests;
//
// public class AngleSharpHtmlQueryTests
// {
//     private const string SampleHtml = """
// <!DOCTYPE html>
// <html>
// <body>
//     <div class="container">
//         <span class="first" data-id="1"> First </span>
//         <span class="second" data-id="2">Second</span>
//         <p>Paragraph</p>
//     </div>
//     <footer>  footer text </footer>
// </body>
// </html>
// """;
//
//     private AngleSharpHtmlQuery _sut = null!;
//
//     [SetUp]
//     public void Setup()
//     {
//         _sut = new AngleSharpHtmlQuery();
//     }
//
//     [Test]
//     public async Task QueryAsync_ReturnsFirstMatchFromFirstSuccessfulSelector()
//     {
//         var selectors = new[] { ".missing", ".second", ".first" };
//
//         var result = await _sut.QueryAsync(SampleHtml, selectors, CancellationToken.None);
//
//         Assert.That(result.Selector, Is.EqualTo(".second"));
//         Assert.That(result.TagName, Is.EqualTo("SPAN"));
//         Assert.That(result.Value, Does.Contain("class=\"second\""));
//         Assert.That(result.Attributes, Has.Exactly(1)
//             .Matches<KeyValuePair<string, string?>>(pair => pair is { Key: "class", Value: "second" }));
//     }
//
//     [Test]
//     public async Task QueryAsync_ReturnsEmptyResultWhenNoSelectorsMatch()
//     {
//         var result = await _sut.QueryAsync(SampleHtml, [], CancellationToken.None);
//
//         Assert.That(result.Selector, Is.Null);
//         Assert.That(result.TagName, Is.Null);
//         Assert.That(result.Value, Is.Empty);
//         Assert.That(result.Attributes, Is.Empty);
//     }
//
//     [Test]
//     public async Task QueryAllAsync_SkipsInvalidSelectorsAndUsesFirstWithResults()
//     {
//         var selectors = new[] { "[", ".second", "span" };
//
//         var results = await _sut.QueryAllAsync(SampleHtml, selectors, CancellationToken.None);
//
//         Assert.That(results, Has.Length.EqualTo(1));
//         Assert.That(results[0].Selector, Is.EqualTo(".second"));
//         Assert.That(results[0].TagName, Is.EqualTo("SPAN"));
//         Assert.That(results[0].Value, Does.Contain("data-id=\"2\""));
//     }
//
//     [Test]
//     public async Task QueryAllAsync_ReturnsEmptyWhenHtmlBlank()
//     {
//         var results = await _sut.QueryAllAsync(string.Empty, ["div"], CancellationToken.None);
//
//         Assert.That(results, Is.Empty);
//     }
//
//     [Test]
//     public async Task QueryAllAsync_HonorsCancellationToken()
//     {
//         using var cts = new CancellationTokenSource();
//         cts.Cancel();
//
//         var results = await _sut.QueryAllAsync(SampleHtml, ["div"], cts.Token);
//
//         Assert.That(results, Is.Empty);
//     }
//
//     [Test]
//     public async Task QueryAllAsync_UsesUtf8WrapperOverload()
//     {
//         IUtf8EncodedHtmlString wrapper = new Utf8EncodedHtmlString { Value = SampleHtml };
//
//         var results = await _sut.QueryAllAsync(wrapper, [".second"], CancellationToken.None);
//
//         Assert.That(results, Has.Length.EqualTo(1));
//         Assert.That(results[0].Selector, Is.EqualTo(".second"));
//     }
//
//     [Test]
//     public async Task RemoveQueryAsync_RemovesMatchesAndReturnsRenderedHtml()
//     {
//         var selectors = new[] { " .first ", "p" };
//
//         var result = await _sut.RemoveQueryAsync(SampleHtml, selectors, CancellationToken.None);
//
//         Assert.That(result.Selector, Is.EqualTo(".first, p"));
//         Assert.That(result.TagName, Is.Null);
//         Assert.That(result.Attributes, Is.Empty);
//         Assert.That(result.Value, Does.Not.Contain("class=\"first\""));
//         Assert.That(result.Value, Does.Not.Contain("<p>Paragraph</p>"));
//         Assert.That(result.Value, Does.Contain("class=\"second\""));
//     }
//
//     [Test]
//     public async Task RemoveQueryAsync_ReturnsOriginalWhenSelectorsNormalizeToEmpty()
//     {
//         var result = await _sut.RemoveQueryAsync(SampleHtml, [], CancellationToken.None);
//
//         Assert.That(result.Selector, Is.Null);
//         Assert.That(result.TagName, Is.Null);
//         Assert.That(result.Value, Is.EqualTo(SampleHtml));
//     }
//
//     [Test]
//     public async Task RemoveQueryAsync_ReturnsOriginalHtmlOnParseFailure()
//     {
//         const string invalidHtml = "   ";
//
//         var result = await _sut.RemoveQueryAsync(invalidHtml, ["div"], CancellationToken.None);
//
//         Assert.That(result.Selector, Is.Null);
//         Assert.That(result.TagName, Is.Null);
//         Assert.That(result.Attributes, Is.Empty);
//         Assert.That(result.Value, Is.EqualTo(invalidHtml));
//     }
//
//     [Test]
//     public async Task MinifyQueryAsync_MinifiesMatchedElements()
//     {
//         const string html = "<div><section id=\"target\">\n    <span>Value</span>\n</section></div>";
//
//         var results = await _sut.MinifyQueryAsync(html, ["section"], CancellationToken.None);
//
//         Assert.That(results, Has.Length.EqualTo(1));
//         Assert.That(results[0].Selector, Is.EqualTo("section"));
//         Assert.That(results[0].Value, Does.Not.Contain("\n"));
//
//         var normalized = string.Concat(results[0].Value.Where(c => !char.IsWhiteSpace(c)));
//         Assert.That(normalized, Is.EqualTo("<sectionid=target><span>Value</span></section>"));
//     }
//
//     [Test]
//     public async Task MinifyQueryAsync_ReturnsEmptyWhenNoMatchesFound()
//     {
//         var results = await _sut.MinifyQueryAsync(SampleHtml, ["article"], CancellationToken.None);
//
//         Assert.That(results, Is.Empty);
//     }
// }
//
// public class Utf8EncodedHtmlStringTests
// {
//     [Test]
//     public void ToString_ReturnsUnderlyingValue()
//     {
//         const string value = "<p>content</p>";
//         var htmlString = new Utf8EncodedHtmlString { Value = value };
//
//         Assert.That(htmlString.ToString(), Is.EqualTo(value));
//         Assert.That(htmlString.Attributes, Is.Empty);
//     }
// }
