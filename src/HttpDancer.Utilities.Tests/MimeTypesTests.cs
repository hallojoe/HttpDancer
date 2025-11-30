using HttpDancer.Utilities;

namespace HttpDancer.Utilities.Tests;

public class MimeTypesTests
{
    [TestCase("jpg", "image/jpeg")]
    [TestCase(".css", "text/css")]
    [TestCase("xml", "application/xml")]
    [TestCase("PDF", "application/pdf")]
    [TestCase("woff2", "font/woff2")]
    public void GetMimeType_KnownExtension_ReturnsMimeType(string extension, string expected)
    {
        var result = MimeTypes.GetMimeType(extension);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("unknown")]
    public void GetMimeType_UnknownOrEmpty_ReturnsNull(string? extension)
    {
        var result = MimeTypes.GetMimeType(extension);

        Assert.That(result, Is.Null);
    }

    [TestCase("application/json", "json")]
    [TestCase("IMAGE/PNG", "png")]
    [TestCase("text/xml", "xml")]
    [TestCase("application/xhtml+xml", "xhtml")]
    public void GetExtension_KnownMimeType_ReturnsExtension(string mimeType, string expected)
    {
        var result = MimeTypes.GetExtension(mimeType);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("application/x-custom")]
    public void GetExtension_UnknownOrEmpty_ReturnsBin(string? mimeType)
    {
        var result = MimeTypes.GetExtension(mimeType);

        Assert.That(result, Is.EqualTo("bin"));
    }
}
