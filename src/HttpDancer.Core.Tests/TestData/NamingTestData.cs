using System.Text.Json;
using HttpDancer.Core.Naming;

namespace HttpDancer.Core.Tests.TestData;

public static class NamingTestData
{
    private static readonly Lazy<IReadOnlyList<NamedCaseSet>> CachedCases = new(Load);

    public static IEnumerable<TestCaseData> GetCases()
    {
        foreach (var caseSet in CachedCases.Value)
        {
            var options = LoadOptions(caseSet.Config);
            foreach (var testCase in caseSet.Cases)
            {
                var testCaseData = new TestCaseData(caseSet.Config, options, testCase)
                    .SetName($"{Path.GetFileNameWithoutExtension(caseSet.Config)} | {testCase.Url}");
                yield return testCaseData;
            }
        }
    }

    private static IReadOnlyList<NamedCaseSet> Load()
    {
        var root = GetTestDataDirectory();
        var casesPath = Path.Combine(root, "cases.json");
        var json = File.ReadAllText(casesPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var parsed = JsonSerializer.Deserialize<List<NamedCaseSet>>(json, options);
        return parsed ?? [];
    }

    private static UrlNamingOptions LoadOptions(string relativeConfigPath)
    {
        var root = GetTestDataDirectory();
        var fullPath = Path.Combine(root, relativeConfigPath.Replace('/', Path.DirectorySeparatorChar));
        var json = File.ReadAllText(fullPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var parsed = JsonSerializer.Deserialize<UrlNamingOptions>(json, options);
        return parsed ?? UrlNamingOptions.Default;
    }

    private static string GetTestDataDirectory()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        return Path.Combine(dir, "..", "..", "..", "TestData");
    }
}

public sealed class NamedCaseSet
{
    public string Config { get; set; } = string.Empty;

    public List<UrlCase> Cases { get; set; } = [];
}

public sealed class UrlCase
{
    public string Url { get; set; } = string.Empty;

    public string? ExpectedPath { get; set; }

    public string? ExpectedName { get; set; }

    public string? ExpectedPathStartsWith { get; set; }

    public string? ExpectedNameStartsWith { get; set; }

    public int? ExpectedQueryHashLength { get; set; }
}
