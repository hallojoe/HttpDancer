using HttpDancer.Core.Configuration;
using HttpDancer.Core.Observability;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Http;

public sealed class CorrelationIdHandler(
    ICorrelationIdProvider correlationIdProvider,
    IOptionsMonitor<ApiClientSettings> apiClientSettings)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var settings = apiClientSettings.CurrentValue;

        if (!settings.IncludeCorrelationIdHeader)
        {
            return base.SendAsync(request, cancellationToken);
        }

        var headerName = settings.CorrelationIdHeaderName;

        // Do not override if caller already set it.
        if (request.Headers.Contains(headerName)) return base.SendAsync(request, cancellationToken);
        
        var correlationId = correlationIdProvider.GetCorrelationId();

        request.Headers.TryAddWithoutValidation(headerName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}