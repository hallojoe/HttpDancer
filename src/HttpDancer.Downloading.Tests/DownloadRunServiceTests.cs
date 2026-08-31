using HttpDancer.Core.Http.Clients;
using HttpDancer.Core.Http;
using HttpDancer.Core;
using HttpDancer.Downloading;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.KnownMediaTypes;
using HttpDancer.Naming;
using HttpDancer.Parsing;
using HttpDancer.Scheduling.RatedScheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace HttpDancer.Downloading.Tests;

public sealed class DownloadRunServiceTests
{
    [Test]
    public async Task ResponseProcessor_DoesNotReadBodyWhenPolicyRejectsContentType()
    {
        var request = new SerializableRequestMessage { Uri = new Uri("https://example.test/image.png") };
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, request.Uri),
            Content = new ByteArrayContent([1, 2, 3])
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        var result = await new HttpResponseMessageProcessor(NullLogger<HttpResponseMessageProcessor>.Instance)
            .ProcessAsync(request, response, "test", request.Uri, true, true, _ => Task.FromResult<bool?>(false), CancellationToken.None);

        Assert.That(result.BodyBytes, Is.Null);
        Assert.That(result.ContentType, Is.EqualTo("image/png"));
    }

    [Test]
    public void AngleSharpLinkParser_ExtractsOnlyValidSameHostHtmlLinks()
    {
        const string html = """
            <a href="/valid/">Valid</a>
            <a href="mailto:edc@edc.dk">Email</a>
            <script>element.src=R+\"/\"+D+\".js\";</script>
            <script type="application/json">{"href":"/not-a-dom-link/"}</script>
            <a href="https://other.example/path">External</a>
            <img src="/image.svg">
            """;

        var links = new AngleSharpLinkParser().GetLinks(html, "https://www.edc.dk/");

        Assert.That(links.Select(link => link.Uri.AbsoluteUri), Is.EquivalentTo(new[]
        {
            "https://www.edc.dk/valid/",
            "https://www.edc.dk/image.svg"
        }));
    }

    [Test]
    public async Task RunAsync_ArchivesDeduplicatedManifestResponsesAndResults()
    {
        var root = Path.Combine(Path.GetTempPath(), $"httpdancer-tests-{Guid.NewGuid():N}");
        try
        {
            var seed = new Uri("https://example.test/sitemap.xml");
            var client = Substitute.For<IHttpClient>();
            client.SendAsync(Arg.Any<SerializableRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(CreateResponse(call.Arg<SerializableRequestMessage>(), seed)));

            var namer = Substitute.For<IUrlNamer>();
            namer.GetNameAndPath(Arg.Any<Uri>()).Returns(call =>
            {
                var uri = call.Arg<Uri>();
                return new UrlNamingResult { FullPath = uri.AbsolutePath.Trim('/').Replace('/', '_'), Name = "index" };
            });
            var mediaTypes = Substitute.For<IKnowMediaTypes>();
            mediaTypes.GetExtension(Arg.Any<string?>()).Returns("html");

            var service = new DownloadRunService(
                client,
                new HttpFileParser(new LinkParser()),
                new HttpFileRenderer(),
                new RatedScheduleFactory(),
                new RatedScheduleRunner(),
                namer,
                mediaTypes,
                Settings(),
                NullLogger<DownloadRunService>.Instance);

            var result = await service.RunAsync(new DownloadRunRequest(seed, new DownloadOptions
            {
                Workspace = root,
                MaxRequestsPerSeed = 2,
                Rate = 100,
                RateWindow = TimeSpan.FromMilliseconds(1),
                MaxDegreeOfParallelism = 2
            }));

            Assert.That(result.SeedSucceeded, Is.True);
            Assert.That(result.ManifestRequestCount, Is.EqualTo(2));
            Assert.That(result.DownloadedCount, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(result.RunDirectory, "source.http")), Is.True);
            Assert.That(File.Exists(Path.Combine(result.RunDirectory, "results.http")), Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(result.RunDirectory, "content"), "*.meta.json", SearchOption.AllDirectories), Has.Length.EqualTo(2));
            Assert.That(Directory.GetFiles(Path.Combine(result.RunDirectory, "content"), "*.html", SearchOption.AllDirectories), Has.Length.EqualTo(2));

            var downloadedUris = client.ReceivedCalls()
                .Select(call => call.GetArguments()[0])
                .OfType<SerializableRequestMessage>()
                .Select(request => request.Uri)
                .Where(uri => !uri.Equals(seed))
                .ToArray();
            Assert.That(downloadedUris, Is.EquivalentTo(new[]
            {
                new Uri("https://example.test/one"),
                new Uri("https://example.test/two")
            }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task RunAsync_ExcludesConfiguredDisallowedUrlPatternsBeforeWritingManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"httpdancer-tests-{Guid.NewGuid():N}");
        try
        {
            var seed = new Uri("https://example.test/");
            var client = Substitute.For<IHttpClient>();
            client.SendAsync(Arg.Any<SerializableRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(CreateResponse(call.Arg<SerializableRequestMessage>(), seed)));
            var namer = Substitute.For<IUrlNamer>();
            var mediaTypes = Substitute.For<IKnowMediaTypes>();
            mediaTypes.GetExtension(Arg.Any<string?>()).Returns("html");
            var service = new DownloadRunService(client, new HttpFileParser(new LinkParser()), new HttpFileRenderer(),
                new RatedScheduleFactory(), new RatedScheduleRunner(), namer, mediaTypes,
                Settings("*.js, *.css, *.svg, *.manifest, *.map"), NullLogger<DownloadRunService>.Instance);

            var result = await service.RunAsync(new DownloadRunRequest(seed, new DownloadOptions
            {
                Workspace = root,
                MaxRequestsPerSeed = 10,
                Rate = 100,
                RateWindow = TimeSpan.FromMilliseconds(1),
                MaxDegreeOfParallelism = 1
            }));

            Assert.That(result.ManifestRequestCount, Is.EqualTo(3));
            var source = await File.ReadAllTextAsync(Path.Combine(result.RunDirectory, "source.http"));
            Assert.That(source, Does.Not.Contain(".js"));
            Assert.That(source, Does.Not.Contain(".css"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task RunAsync_RecordsSeedTransportFailureWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"httpdancer-tests-{Guid.NewGuid():N}");
        try
        {
            var seed = new Uri("https://example.test/sitemap.xml");
            var client = Substitute.For<IHttpClient>();
            client.SendAsync(Arg.Any<SerializableRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(new CompletedHttpResponseMessage
                {
                    SerializableRequest = call.Arg<SerializableRequestMessage>(),
                    Uri = seed,
                    Method = "GET",
                    StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                    Message = "TLS failed"
                }));

            var namer = Substitute.For<IUrlNamer>();
            var mediaTypes = Substitute.For<IKnowMediaTypes>();
            var service = new DownloadRunService(client, new HttpFileParser(new LinkParser()), new HttpFileRenderer(),
                new RatedScheduleFactory(), new RatedScheduleRunner(), namer, mediaTypes, Settings(), NullLogger<DownloadRunService>.Instance);

            var result = await service.RunAsync(new DownloadRunRequest(seed, new DownloadOptions { Workspace = root }));

            Assert.That(result.SeedSucceeded, Is.False);
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(result.RunDirectory, "seed.meta.json")), Is.True);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static CompletedHttpResponseMessage CreateResponse(SerializableRequestMessage request, Uri seed)
    {
        var isSeed = request.Uri == seed;
        var body = isSeed
            ? "https://example.test/one https://example.test/one https://example.test/two https://example.test/three https://example.test/app.js https://example.test/site.css"
            : "downloaded";
        return new CompletedHttpResponseMessage
        {
            SerializableRequest = request,
            Uri = request.Uri,
            Method = request.Method.Method,
            StatusCode = System.Net.HttpStatusCode.OK,
            ContentType = "text/html",
            BodyBytes = System.Text.Encoding.UTF8.GetBytes(body),
            CorrelationId = "test"
        };
    }

    private static IOptionsMonitor<HttpDancerSettings> Settings(string? disallowedUrlPattern = null)
    {
        var settings = Substitute.For<IOptionsMonitor<HttpDancerSettings>>();
        settings.CurrentValue.Returns(new HttpDancerSettings
        {
            DefaultClient = new HttpDancerClientSettings { DisallowedUrlPattern = disallowedUrlPattern }
        });
        return settings;
    }
}
