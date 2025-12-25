using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.Parsing.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddParsing(this IServiceCollection services)
    {
        services.AddSingleton<ILinkParser, LinkParser>();
        services.AddSingleton<IDateTimeParser, DateTimeParser>();

        return services;
    }
}
