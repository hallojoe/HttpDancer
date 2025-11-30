using HttpDancer.Extensions;

namespace HttpDancer.Extensions.Tests;

public class ShortHashExtensionsTests
{
    [Test]
    public void ToShortHash_ProducesExpectedValueForKnownInput()
    {
        var result = "hello world".ToShortHash();

        Assert.That(result, Is.EqualTo("xfgspomtju7aq"));
    }

    [TestCase(1, 2)]
    [TestCase(4, 7)]
    [TestCase(8, 13)]
    [TestCase(16, 26)]
    public void ToShortHash_RespectsRequestedByteLength(int bytes, int expectedLength)
    {
        var hash = "variable input".ToShortHash(bytes);

        Assert.That(hash.Length, Is.EqualTo(expectedLength));
        Assert.That(hash, Is.EqualTo(hash.ToLowerInvariant()));
    }

    [Test]
    public void ToShortHash_DifferentInputsProduceDifferentHashes()
    {
        var first = "alpha".ToShortHash();
        var second = "beta".ToShortHash();

        Assert.That(first, Is.Not.EqualTo(second));
    }
}
