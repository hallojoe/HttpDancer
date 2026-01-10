using System.Diagnostics;
using System.Text;
using HttpDancer.Console.Workflows;
using HttpDancer.Core.Http.Clients;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.Scheduling.RatedScheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        // var urlNamingOptions = scope.ServiceProvider.GetRequiredService<IOptions<UrlNamingOptions>>();
        // var urlNameProvider = scope.ServiceProvider.GetRequiredService<IUrlNamer>();
        // var fileSystemDownloaderSettings = scope.ServiceProvider.GetRequiredService<IOptions<FileSystemDownloaderSettings>>();
        // var dataProvider = scope.ServiceProvider.GetRequiredService<IFileSystemDataProvider>();
        // var ratedScheduleFactory = scope.ServiceProvider.GetRequiredService<IRatedScheduleFactory>();
        // var ratedScheduleRunner = scope.ServiceProvider.GetRequiredService<IRatedScheduleRunner>();
        // var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClient>();
        // var httpFileFactory = scope.ServiceProvider.GetRequiredService<HttpFileFactory>();
        var httpFileRenderer = scope.ServiceProvider.GetRequiredService<IHttpFileRenderer>();
        // var httpFileDocumentFromUrl = await httpFileFactory.CreateAsync(new Uri("https://danbolig.dk/sitemap.website.xml"));
        // var httpFileDocumentAsStringFromUnStructuredDocument2 = httpFileRenderer.Render(
        //     httpFileDocumentFromUrl,
        //     new HttpFileRenderOptions(true, false));
        //
        // await File.WriteAllTextAsync(
        //     Path.Combine(Directory.GetCurrentDirectory(), "danbolig.http.rendered.html"), 
        //     httpFileDocumentAsStringFromUnStructuredDocument2,
        //     Encoding.UTF8);
        
        var ratedHttpFileRunner = scope.ServiceProvider.GetRequiredService<RatedHttpFileRunner>();

        // var xprocessedHttpFileDocument = await ratedHttpFileRunner.Run(
        //     new Uri("https://digst.dk/sitemap/"), 
        //     1, 
        //     TimeSpan.FromSeconds(3), 
        //     CancellationToken.None);

        var processedHttpFileDocument = await ratedHttpFileRunner.Run(
            new Uri("https://vitusguld.dk"), 
            1, 
            TimeSpan.FromSeconds(30), 
            CancellationToken.None);
        
        var processedHttpFileDocumentString = httpFileRenderer.Render(
            processedHttpFileDocument,
            new HttpFileRenderOptions(true, false));

        await File.WriteAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(), "vitus.dk.http.log"), 
            processedHttpFileDocumentString,
            Encoding.UTF8);

        System.Console.WriteLine("Crawler finished. Press any key to exit...");
        System.Console.ReadKey();
    }
}

