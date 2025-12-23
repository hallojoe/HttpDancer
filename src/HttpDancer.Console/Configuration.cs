using HttpDancer.Core.Configuration;
using HttpDancer.Core.Http;
using HttpDancer.Core.Http.Downloading;
using HttpDancer.FileSystemDownloader.Configuration;
using HttpDancer.Html;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Console;

public static class ConfigurationExtensions
{
    public static HostApplicationBuilder ConfigureApplication(this HostApplicationBuilder builder)
    {
        // Logging setup
        builder.Logging.ClearProviders(); // Remove default providers
        builder.Logging.AddConsole(); // Output logs to console
        builder.Logging.SetMinimumLevel(LogLevel.Information); // Set global log level

        builder.Services.AddOptions<HttpDancerSettings>().BindConfiguration(HttpDancerSettings.Key);
        
        // Configuration app settings
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Dependency Injection

        builder.Services.AddDefaultHttpClient(builder.Configuration);
        builder.Services.AddFileSystemDownloader(builder.Configuration);
        builder.Services.AddUrlNaming(builder.Configuration);

        builder.Services.AddScoped<IHtmlQuery, AngleSharpHtmlQuery>();

        return builder;
    }
}