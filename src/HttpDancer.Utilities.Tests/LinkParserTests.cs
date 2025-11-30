using HttpDancer.Utilities.Parsing;

namespace HttpDancer.Utilities.Tests;

public class LinkParserTests
{
    [Test]
    public void GetLinks_ExtractsHttpAndHttpsFromHtmlAndText()
    {
        const string sourceUrl = "https://contoso.com/blog/index.html";
        const string html = """
<a href="https://contoso.com/absolute">absolute</a>
<img src="/images/logo.png" />
<link href='style.css' />
<p>See https://contoso.com/plain/text for more.</p>
<a href="#section">ignored anchor</a>
<a href="mailto:test@example.com">ignored mailto</a>
<a href="https://contoso.com/absolute">duplicate</a>
<script src="ftp://contoso.com/file.bin"></script>
""";

        var links = LinkParser.GetLinks(html, sourceUrl);

        var uris = links.Select(l => l.Uri.ToString()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(uris, Is.EquivalentTo(new[]
            {
                "https://contoso.com/absolute",
                "https://contoso.com/images/logo.png",
                "https://contoso.com/blog/style.css",
                "https://contoso.com/plain/text"
            }));
            Assert.That(links.All(l => l.SourceUrl == sourceUrl), Is.True);
        });
    }

    [Test]
    public void GetLinks_ThrowsWhenHtmlMissing()
    {
        Assert.That(() => LinkParser.GetLinks("", "https://example.com"), Throws.ArgumentNullException);
    }

    [Test]
    public void GetLinks_ThrowsWhenSourceUrlInvalid()
    {
        const string html = "<a href=\"/test\">test</a>";

        Assert.That(() => LinkParser.GetLinks(html, "not-a-url"), Throws.ArgumentException);
    }
}
