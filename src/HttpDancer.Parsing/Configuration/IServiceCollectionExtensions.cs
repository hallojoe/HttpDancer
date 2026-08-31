using HttpDancer.Parsing.LinkHttpHeaderParser;
using Microsoft.Extensions.DependencyInjection;

namespace HttpDancer.Parsing.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddParsing(this IServiceCollection services)
    {
        services.AddSingleton<ILinkHttpHeaderParser, DefaultLinkHttpHttpHeaderParser>();
        services.AddSingleton<ILinkParser, AngleSharpLinkParser>();
        services.AddSingleton<IDateTimeParser, DateTimeParser>();

        return services;
    }
}
