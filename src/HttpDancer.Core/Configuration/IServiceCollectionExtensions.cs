using HttpDancer.Core.Http.Clients;
using HttpDancer.Core.Http.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpDancerSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HttpDancerSettings>(configuration.GetSection(HttpDancerSettings.Key));
        return services;
    }
    
    public static IServiceCollection AddDefaultHttpClient(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddTransient<CorrelationIdHandler>();
        
        if (configuration is not null)
        {
            services.AddHttpDancerSettings(configuration);
        }

        services.AddHttpClient<IHttpClient, DefaultHttpClient>((serviceProvider, httpClient) =>
        {
            var httpSettings = serviceProvider
                .GetRequiredService<IOptionsMonitor<HttpDancerSettings>>()
                .CurrentValue;

            var httpClientSettings = httpSettings.HttpClient;

            httpClient.Timeout = httpClientSettings.Timeout;
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(httpClientSettings.UserAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var httpDancerSettings = serviceProvider.GetRequiredService<IOptionsMonitor<HttpDancerSettings>>().CurrentValue;
            var handlerSettings = httpDancerSettings.SocketsHttpHandler;

            return new SocketsHttpHandler
            {
                MaxConnectionsPerServer = handlerSettings.MaxConnectionsPerServer,
                PooledConnectionLifetime = handlerSettings.PooledConnectionLifetime,
                PooledConnectionIdleTimeout = handlerSettings.PooledConnectionIdleTimeout,
                AutomaticDecompression = handlerSettings.AutomaticDecompression,
                ConnectTimeout = handlerSettings.ConnectTimeout,
                EnableMultipleHttp2Connections = handlerSettings.EnableMultipleHttp2Connections,
                AllowAutoRedirect = handlerSettings.AllowAutoRedirect,
                UseCookies = handlerSettings.UseCookies,
                KeepAlivePingDelay = handlerSettings.KeepAlivePingDelay,
                KeepAlivePingTimeout = handlerSettings.KeepAlivePingTimeout,
                KeepAlivePingPolicy = handlerSettings.KeepAlivePingPolicy
            };
        })
        .AddHttpMessageHandler<CorrelationIdHandler>();
        
        return services;
    }
}
