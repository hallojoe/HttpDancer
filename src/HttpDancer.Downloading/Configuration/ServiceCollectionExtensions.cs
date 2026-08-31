using HttpDancer.FileFormats.HttpFile;
using HttpDancer.KnownMediaTypes.Configuration;
using HttpDancer.Naming.Configuration;
using HttpDancer.Parsing.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.Downloading.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDownloadWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DownloadOptions>(configuration.GetSection(DownloadOptions.Key));
        services.AddKnownMediaTypes(configuration);
        services.AddUrlNaming(configuration);
        services.AddParsing();
        services.AddSingleton<IHttpFileParser, HttpFileParser>();
        services.AddSingleton<IHttpFileRenderer, HttpFileRenderer>();
        services.AddSingleton<IDownloadRunService, DownloadRunService>();
        return services;
    }
}
