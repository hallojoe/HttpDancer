using HttpDancer.Core.Configuration;
using HttpDancer.FileSystemDownloader.Configuration;
using HttpDancer.Html;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HttpDancer.Console;

public static class ConfigurationExtensions
{
    public static HostApplicationBuilder ConfigureApplication(this HostApplicationBuilder builder)
    {
        // Logging setup
        
        builder.Logging.ClearProviders(); // Remove default providers
        builder.Logging.AddConsole(); // Output logs to console
        builder.Logging.SetMinimumLevel(LogLevel.Information); // Set global log level
        
        // Configuration app settings

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.KnownMediaTypes.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.HtmlMinification.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.HtmlQuerying.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.UrlNaming.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        
        // Dependency Injection

        builder.Services.AddDefaultHttpClient(builder.Configuration);
        builder.Services.AddFileSystemDownloader(builder.Configuration);

        builder.Services.AddScoped<IHtmlQuery, AngleSharpHtmlQuery>();

        return builder;
    }
}