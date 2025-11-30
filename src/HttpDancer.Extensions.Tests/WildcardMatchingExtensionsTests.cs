using HttpDancer.Extensions;

namespace HttpDancer.Extensions.Tests;

public class WildcardMatchingExtensionsTests
{
    [Test]
    public void IsMatch_MatchesWithWildcardsAndCaseInsensitiveByDefault()
    {
        var result = "Report.PDF".IsMatch("*.pdf");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsMatch_HonorsCaseSensitivityWhenRequested()
    {
        var result = "Report.PDF".IsMatch("*.pdf", ignoreCase: false);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsMatch_SupportsSingleAndMultiCharacterWildcards()
    {
        var result = "file01.txt".AsSpan().IsMatch("fi?e*.txt".AsSpan());

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsMatch_BacktracksAcrossMultipleStars()
    {
        var result = "zzzabyyyc".IsMatch("*ab*c");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsAnyMatch_EnumerablePatterns_ReturnsTrueWhenAnyMatches()
    {
        var result = WildcardMatchingExtensions.IsAnyMatch(
            "image/png",
            new[] { "text/*", "audio/*", "image/*" });

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsAnyMatch_CommaSeparatedPatterns_TrimsSegments()
    {
        var result = "application/json".IsAnyMatch(" text/* ,image/* , application/json ");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsMatch_CommaSeparatedPatterns_OnUrlMatchesHost()
    {
        var result = "https://www.sst.dk/da/Borger".IsMatch("sst.dk, *.sst.dk");

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsMatch_SupportsHostWildcardPrefixes()
    {
        Assert.That("api.example.com".IsMatch("*.example.com"), Is.True);
        Assert.That("example.com".IsMatch("*.example.com"), Is.True);
        Assert.That("example.org".IsMatch("*.example.com"), Is.False);
    }

    [Test]
    public void IsMatch_StringInput_NullArgumentsThrow()
    {
        Assert.That(() => WildcardMatchingExtensions.IsMatch(null!, "*"), Throws.TypeOf<ArgumentNullException>());
        Assert.That(() => WildcardMatchingExtensions.IsMatch("input", null!), Throws.TypeOf<ArgumentNullException>());
    }

    // [Test]
    // public void IsAnyMatch_NullArgumentsThrow()
    // {
    //     Assert.That(() => WildcardMatchingExtensions.IsAnyMatch(null!, new[] { "*" }), Throws.TypeOf<ArgumentNullException>());
    //     Assert.That(() => WildcardMatchingExtensions.IsAnyMatch("input", null!), Throws.TypeOf<ArgumentNullException>());
    // }
}
