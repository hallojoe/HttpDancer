using HttpDancer.Core.Parsing;
using HttpDancer.FileSystemDownloader.Data;
using HttpDancer.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.FileSystemDownloader.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileSystemDownloader(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HtmlParsingSettings>(configuration.GetSection(HtmlParsingSettings.Key));
        services.Configure<FileSystemDownloaderSettings>(configuration.GetSection(FileSystemDownloaderSettings.Key));

        
        services.AddOptions<MinificationSettings>().BindConfiguration(MinificationSettings.Key);
        services.AddSingleton<IDateTimeParser, DateTimeParser>();
        services.AddSingleton<IHtmlStringsProvider, HtmlStringsParser>();
        services.AddSingleton<IFileSystemDownloadRunner, FileSystemDownloadRunner>();
        services.AddSingleton<IFileSystemDataProvider, FileSystemDataProvider>();
        services.AddSingleton<IUrlProvider, FileSystemUrlProvider>();
        return services;
    }
}
