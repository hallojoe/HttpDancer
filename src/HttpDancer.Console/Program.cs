using HttpDancer.FileSystemDownloader;
using HttpDancer.FileSystemDownloader.Configuration;
using HttpDancer.Naming;
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

        // 
        
        // Build host
        using var host = builder.Build();

        // Create a DI scope
        using var scope = host.Services.CreateScope();

        // Get required services
        // var urlNamingOptions = scope.ServiceProvider.GetRequiredService<IOptions<UrlNamingOptions>>();
        // var urlNameProvider = scope.ServiceProvider.GetRequiredService<IUrlNamer>();
        // var fileSystemDownloaderSettings = scope.ServiceProvider.GetRequiredService<IOptions<FileSystemDownloaderSettings>>();
        // var dataProvider = scope.ServiceProvider.GetRequiredService<IFileSystemDataProvider>();

        var fileSystemDownloadRunner = scope.ServiceProvider.GetRequiredService<IFileSystemDownloadRunner>();

        var urlProvider = scope.ServiceProvider.GetRequiredService<IUrlProvider>();
        
        var urls = await urlProvider.GetUrlsAsync();

        // var urlNames = urls.Select(x => urlNameProvider.GetNameAndPath(x)).ToList();
     
        var _ = await fileSystemDownloadRunner.Run(urls, true);

        
        // var dataFiles = Directory.GetFiles(fileSystemDownloaderSettings.Value.Workspace!, "*.data.json", SearchOption.AllDirectories);

        // var categories = new List<string>();
        // foreach (var dataFile in dataFiles)
        // {
        //     var dataContent = await dataProvider.ReadStringAsync(dataFile.Replace(fileSystemDownloaderSettings.Value.Workspace!, ""));
        //     if (string.IsNullOrWhiteSpace(dataContent))
        //     {
        //         continue;
        //     }
        //     
        //     var data = JsonSerializer.Deserialize<Dictionary<string, string?>>(dataContent);
        //     if (data is null)
        //     {
        //         continue;
        //     }
        //
        //     if (data.TryGetValue("pageTag", out var value))
        //     {
        //         if(!string.IsNullOrWhiteSpace(value?.Trim())) categories.Add(value.Trim());
        //     }
        //
        // }

        // await dataProvider.WriteStringAsync("categories.txt", string.Join(Environment.NewLine, categories.Distinct().ToArray()));
        
        
        System.Console.WriteLine("Crawler finished. Press any key to exit...");
        System.Console.ReadKey();
    }
}

