using HttpDancer.Utilities.IO;

namespace HttpDancer.Utilities.Tests;

public class IoHelperTests
{
    [Test]
    public void GetFiles_ReturnsMatchingFilesAcrossDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HttpDancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var subDir = Path.Combine(tempDir, "sub");
            Directory.CreateDirectory(subDir);

            var txt1 = Path.Combine(tempDir, "a.txt");
            var txt2 = Path.Combine(subDir, "b.txt");
            var other = Path.Combine(subDir, "c.log");

            File.WriteAllText(txt1, "a");
            File.WriteAllText(txt2, "b");
            File.WriteAllText(other, "c");

            var result = IoHelper.GetFiles(tempDir, "*.txt");

            Assert.That(result, Is.EquivalentTo(new[] { txt1, txt2 }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task ReadAndWriteString_RoundTripsUtf8Async()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HttpDancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "content.txt");

        try
        {
            const string content = "Hello UTF8!";
            await IoHelper.WriteStringAsync(filePath, content);

            var read = await IoHelper.ReadString(filePath);

            Assert.That(read, Is.EqualTo(content));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task ReadAllLinesAsync_TrimsDistinctAndFiltersEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HttpDancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "lines.txt");

        try
        {
            await File.WriteAllLinesAsync(filePath, new[]
            {
                " first ",
                "",
                "second",
                "first"
            });

            var result = await IoHelper.ReadAllLinesAsync(tempDir, "lines.txt");

            Assert.That(result, Is.EqualTo(new[] { "first", "second" }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public async Task ReadAllLinesAsync_MultipleFilesAggregateDistinct()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HttpDancerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "a.txt"), new[] { "one", "two" });
            await File.WriteAllLinesAsync(Path.Combine(tempDir, "b.txt"), new[] { "two", "three" });

            var result = await IoHelper.ReadAllLinesAsync(tempDir, new[] { "a.txt", "b.txt" });

            Assert.That(result, Is.EquivalentTo(new[] { "one", "two", "three" }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public void EnsureMaxFileNameLength_TruncatesFromLeftAndKeepsExtension()
    {
        var baseName = new string('a', 260);
        var fileName = baseName + ".txt";

        var result = IoHelper.EnsureMaxFileNameLength(fileName, maxLength: 255);
        var expected = new string('a', 251) + ".txt";

        Assert.Multiple(() =>
        {
            Assert.That(result.Length, Is.EqualTo(255));
            Assert.That(result.EndsWith(".txt", StringComparison.Ordinal), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void EnsureMaxFileNameLength_ThrowsWhenNameMissing()
    {
        Assert.That(() => IoHelper.EnsureMaxFileNameLength(string.Empty), Throws.ArgumentException);
    }
}
