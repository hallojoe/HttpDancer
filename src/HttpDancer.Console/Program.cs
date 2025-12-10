using HttpDancer.Core.Configuration;
using HttpDancer.FileSystemDownloader;
using HttpDancer.FileSystemDownloader.Configuration;
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
        var fileSystemDownloadRunner = scope.ServiceProvider.GetRequiredService<IFileSystemDownloadRunner>();
        var urlProvider = scope.ServiceProvider.GetRequiredService<IUrlProvider>();
        
        var urls = await urlProvider.GetUrlsAsync();
        var _ = await fileSystemDownloadRunner.Run(urls, false);
        
        System.Console.WriteLine("Crawler finished. Press any key to exit...");
        System.Console.ReadKey();
    }
}

