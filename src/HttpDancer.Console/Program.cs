using HttpDancer.Core.Configuration;
using HttpDancer.FileSystemDownloader;
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
        var httpDancerSettingsOptionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<HttpDancerSettings>>();

        var fileSystemDataProvider = scope.ServiceProvider.GetRequiredService<IFileSystemDataProvider>();
        var fileSystemDownloadRunner = scope.ServiceProvider.GetRequiredService<IFileSystemDownloadRunner>();
        var fileSystemDataSettings = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<FileSystemDataSettings>>();
        
        // Read URLs from options 
        var urlsWithNoQuerystring = (await fileSystemDataProvider.ReadAllLinesAsync(fileSystemDataSettings.CurrentValue.HrefListPaths))
            .Select(url => url.Split('?').First()).Distinct().ToArray();
        
        var y = await fileSystemDownloadRunner.Run(urlsWithNoQuerystring, true);
        
        System.Console.WriteLine("Crawler finished. Press any key to exit...");
        System.Console.ReadKey();
    }
}

