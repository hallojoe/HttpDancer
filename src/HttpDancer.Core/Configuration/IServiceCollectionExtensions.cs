using HttpDancer.Core.Http;
using HttpDancer.Core.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpSettings(this IServiceCollection services, IConfiguration configuration)
    {
        // Binds the Http section of appsettings.json to HttpSettings.
        services.Configure<HttpSettings>(configuration.GetSection(HttpSettings.Key));
        return services;
    }
    
    public static IServiceCollection AddDefaultHttpClient(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddTransient<CorrelationIdHandler>();
        
        // Adds HttpSettings to the DI container when configuration is available.
        if (configuration is not null)
        {
            services.AddHttpSettings(configuration);
        }

        // Add the default HTTP client.
        services.AddHttpClient<IHttpClient, DefaultHttpClient>((serviceProvider, httpClient) =>
        {
            // Gets the current HttpSettings from the DI container.
            var httpSettings = serviceProvider
                .GetRequiredService<IOptionsMonitor<HttpSettings>>()
                .CurrentValue;

            // Gets the HttpClientSettings from HttpSettings.
            var httpClientSettings = httpSettings.HttpClient;

            // Throws an exception if the HttpClientSettings are null.
            if (httpClientSettings is null)
            {
                throw new InvalidOperationException("HttpSettings.HttpClient is not configured.");
            }

            // Sets the total allowed time for the ENTIRE HTTP operation:
            // DNS → TCP → TLS → "Request send" → Server wait → Value download.
            // If this time is exceeded, the request throws a TaskCanceledException.
            httpClient.Timeout = TimeSpan.FromSeconds(20);

            // Adds a User-Agent header to all outgoing requests.
            // Some APIs reject requests without a valid UA.
            // This also influences server-side throttling and caching behavior.
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36"
            );
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            // Gets the current SocketsHttpHandler from HttpSettings.
            var httpSettings = serviceProvider.GetRequiredService<IOptionsMonitor<HttpSettings>>().CurrentValue;
            var handlerSettings = httpSettings.SocketsHttpHandler
                ?? throw new InvalidOperationException("HttpSettings.SocketsHttpHandler is not configured.");

            return new SocketsHttpHandler
            {
                // Maximum number of concurrent TCP connections allowed PER (host, scheme, port).
                // If more requests arrive while all connections are busy, they queue until a connection is free.
                MaxConnectionsPerServer = handlerSettings.MaxConnectionsPerServer,

                // Forces connections to be recycled after the specified lifetime.
                // Helps avoid "stale" connections, load-balancer stickiness, and long-lived TCP issues.
                // Shorter lifetimes can help balance the load across servers behind a load balancer.
                PooledConnectionLifetime = handlerSettings.PooledConnectionLifetime,

                // How long an idle (unused) connection is allowed to stay in the pool before being closed.
                // Helps prevent reuse of idle connections that may have been silently closed by the server or a NAT.
                PooledConnectionIdleTimeout = handlerSettings.PooledConnectionIdleTimeout,

                // Automatically decompresses server responses that use GZip or Deflate.
                // Saves bandwidth and avoids manually handling compressed streams.
                AutomaticDecompression = handlerSettings.AutomaticDecompression,

                // Time allowed for establishing the TCP (and TLS) connection only.
                // If the server is slow to accept connections, this fails fast
                // without consuming the full request timeout.
                ConnectTimeout = handlerSettings.ConnectTimeout,

                // Allows more than one HTTP/2 connection per server host.
                // Useful when a server restricts concurrent HTTP/2 streams or throughput is limited.
                // Increasing this can improve parallel performance for heavy HTTP/2 workloads.
                EnableMultipleHttp2Connections = handlerSettings.EnableMultipleHttp2Connections,

                // Prevents automatic following of 3xx redirects.
                // Useful for APIs, scrapers, and debugging where you need full control over redirect behavior.
                AllowAutoRedirect = handlerSettings.AllowAutoRedirect,

                // Disables automatic cookie storage and sending.
                // Prevents state leakage across requests and avoids unintended session persistence.
                UseCookies = handlerSettings.UseCookies,

                // Time the client waits before sending keep-alive pings on HTTP/2/HTTP/3 connections.
                // Helps detect dead connections earlier on networks with NAT or long idle periods.
                KeepAlivePingDelay = handlerSettings.KeepAlivePingDelay,

                // Time allowed to wait for a keep-alive ping response.
                // If no response is received within this window, the connection is treated as dead.
                KeepAlivePingTimeout = handlerSettings.KeepAlivePingTimeout,

                // Defines WHEN keep-alive pings are sent:
                // Always → ping even when there's no active request
                // WithActiveRequests → ping only during request activity
                // Useful to prevent idle connection drops on flaky networks.
                KeepAlivePingPolicy = handlerSettings.KeepAlivePingPolicy
            };
        })
        .AddHttpMessageHandler<CorrelationIdHandler>();
        
        return services;
    }
}
