using HttpDancer.Downloading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HttpDancer.Console;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.ConfigureApplication();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<DownloadOptions>>().Value;
        var seedStrings = args.Length > 0 ? args : options.SeedUrls;
        if (seedStrings.Length == 0)
        {
            System.Console.Error.WriteLine("Supply one or more seed URLs, or configure HttpDancer:Download:SeedUrls.");
            return;
        }

        var runService = scope.ServiceProvider.GetRequiredService<IDownloadRunService>();
        foreach (var seedString in seedStrings)
        {
            if (!Uri.TryCreate(seedString, UriKind.Absolute, out var seedUri) ||
                (seedUri.Scheme != Uri.UriSchemeHttp && seedUri.Scheme != Uri.UriSchemeHttps))
            {
                System.Console.Error.WriteLine($"Invalid seed URL: {seedString}");
                continue;
            }

            System.Console.WriteLine($"Starting seed: {seedUri}");
            var result = await runService.RunAsync(new DownloadRunRequest(seedUri, options), CancellationToken.None);
            System.Console.WriteLine(
                $"Completed seed: {seedUri}. Requests={result.ManifestRequestCount}, " +
                $"Downloaded={result.DownloadedCount}, Failed={result.FailedCount}, Archive={result.RunDirectory}");
        }
    }
}
