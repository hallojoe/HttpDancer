using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.FileSystemDownloader.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileSystemDownloader(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileSystemDataSettings>(configuration.GetSection(FileSystemDataSettings.Key));
        services.AddOptions<MinificationSettings>().BindConfiguration(MinificationSettings.Key);
        services.AddSingleton<IFileSystemDownloadRunner, FileSystemDownloadRunner>();
        services.AddSingleton<IFileSystemDataProvider, FileSystemDataProvider>();
        return services;
    }
}
