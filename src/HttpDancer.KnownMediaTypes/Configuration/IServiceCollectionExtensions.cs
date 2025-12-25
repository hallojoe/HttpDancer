using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.KnownMediaTypes.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKnownMediaTypes(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var optionsBuilder = services.AddOptions<KnownMediaTypes>();
        if (configuration is not null)
        {
            optionsBuilder.Bind(configuration.GetSection(KnownMediaTypes.Key));
        }

        services.AddSingleton<IKnowMediaTypes, MediaTypes>();

        return services;
    }
}
