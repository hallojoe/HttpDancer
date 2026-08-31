using HttpDancer.Core.Configuration;
using HttpDancer.Downloading.Configuration;
using HttpDancer.Scheduling.Configuration;
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
            .AddJsonFile("appsettings.UrlNaming.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        
        // Dependency Injection

        builder.Services.AddHttpDancerSettings(builder.Configuration);
        builder.Services.AddDefaultHttpClient(builder.Configuration);
        builder.Services.AddSchedulingFeatures(builder.Configuration);
        builder.Services.AddDownloadWorkflow(builder.Configuration);

        
        return builder;
    }
}
