using HttpDancer.Naming.Hashing;
using HttpDancer.Naming.Query;
using HttpDancer.Naming.Segments;
using HttpDancer.Naming.Slug;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.Naming.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUrlNaming(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.AddOptions<UrlNamingOptions>().Bind(configuration.GetSection(UrlNamingOptions.Key));
        }
        else
        {
            services.AddOptions<UrlNamingOptions>();
        }
        
        services.AddSingleton<IPathSegmentFilter, PathSegmentFilter>();
        services.AddSingleton<IPathSegmentNormalizer, PathSegmentNormalizer>();
        services.AddSingleton<IHashGenerator, Base62HashGenerator>();
        services.AddSingleton<IQueryStringProcessor, QueryStringProcessor>();
        services.AddSingleton<ISlugify, Slugify>();
        services.AddSingleton<IUrlNamer, UrlNamer>();

        return services;
    }
}
