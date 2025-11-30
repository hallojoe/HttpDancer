using HttpDancer.Core.Configuration;
using HttpDancer.Utilities.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HttpDancer.Console;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Create the host builder manually
        var builder = Host.CreateApplicationBuilder(args);

        // Configure application
        builder.ConfigureApplication();

        // Build host
        using var host = builder.Build();

        // Create a DI scope
        using var scope = host.Services.CreateScope();

        // Get required services
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiClientSettings>>();

        var downloadHtmlJob = scope.ServiceProvider.GetRequiredService<DownloadHtmlJob>();
        
        // Read URLs from options 
        var urls = await IoHelper.ReadAllLinesAsync(options.Value.Io.Workspace ?? throw new ArgumentNullException(), options.Value.Io.HrefListPaths);
        
        var y = await downloadHtmlJob.DownloadHtml(urls, false);
        
        System.Console.WriteLine("Crawler finished. Press any key to exit...");
        System.Console.ReadKey();
    }
}
