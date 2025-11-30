using HttpDancer.Core.Configuration;
using HttpDancer.Core.Http;
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

        builder.Services.AddOptions<ApiClientSettings>().BindConfiguration(ApiClientSettings.Key);

        var apiClientSettings = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<ApiClientSettings>>().Value;
        
        // Configuration app settings
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            //.AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        
        // builder.Services.AddHttpClient<IHttpClient, DefaultHttpClient>();

        // Dependency Injection

        builder.Services.AddDefaultHttpClient(builder.Configuration);

        
        // Add download service
        builder.Services.AddTransient<IDownloadService, ChanneledDownloadService>();

        builder.Services.AddTransient<DownloadHtmlJob>();
        

        return builder;
    }
}