using HttpDancer.FileSystemDownloader.Data;
using HttpDancer.FileSystemDownloader.Minification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.FileSystemDownloader.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileSystemDownloader(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileSystemDownloaderSettings>(configuration.GetSection(FileSystemDownloaderSettings.Key));
        services.AddOptions<MinificationSettings>().BindConfiguration(MinificationSettings.Key);
        services.AddSingleton<IHtmlMetaTagProvider, AngleSharpHtmlMetaTagProvider>();
        services.AddSingleton<IHtmlMinifier, AngleSharpHtmlMinifier>();
        services.AddSingleton<IHtmlStringsProvider, AngleSharpHtmlStringsProvider>();
        services.AddSingleton<IFileSystemDownloadRunner, FileSystemDownloadRunner>();
        services.AddSingleton<IFileSystemDataProvider, FileSystemDataProvider>();
        services.AddSingleton<IUrlProvider, FileSystemUrlProvider>();
        return services;
    }
}
