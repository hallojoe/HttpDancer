using System.Diagnostics;
using HttpDancer.Core.Http.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Http.Clients;

public class DefaultHttpClient(
    ILogger<DefaultHttpClient> logger,
    IOptionsMonitor<HttpDancerSettings> apiClientSettings,
    ICorrelationIdProvider correlationIdProvider, 
    IHttpResponseMessageProcessor responseMessageProcessor,
    HttpClient httpClient) : IHttpClient
{
    #region Public generic entrypoint

    public Task<CompletedHttpResponseMessage> SendAsync(SerializableRequestMessage serializableRequestMessage, CancellationToken cancellationToken)
        => SendInternalAsync(serializableRequestMessage, cancellationToken);

    #endregion

    #region Core Send Logic
    
    private async Task<CompletedHttpResponseMessage> SendInternalAsync(
        SerializableRequestMessage serializableRequestMessage,
        CancellationToken cancellationToken)
    {
        var uri = serializableRequestMessage.Uri ?? throw new ArgumentNullException(nameof(serializableRequestMessage.Uri));
        var method = serializableRequestMessage.Method ?? throw new ArgumentNullException(nameof(serializableRequestMessage.Method));

        var correlationId = correlationIdProvider.GetCorrelationId();

        logger.LogDebug(
            "Sending {Method} serializableRequestMessage to {Uri} (CorrelationId={CorrelationId})",
            method,
            uri,
            correlationId);

        var settings = apiClientSettings.CurrentValue;

        logger.LogDebug(
            "ApiClient settings snapshot for {Method} {Uri} (CorrelationId={CorrelationId}): ReadBodyOnSuccess={ReadBodyOnSuccess}, ReadBodyOnNonSuccess={ReadBodyOnNonSuccess}",
            method,
            uri,
            correlationId,
            settings.DefaultClient.ReadBodyOnSuccess,
            settings.DefaultClient.ReadBodyOnNonSuccess);

        using var httpRequest = new HttpRequestMessage(method, uri);

        if (serializableRequestMessage.Content is not null)
        {
            httpRequest.Content = serializableRequestMessage.Content;
            logger.LogDebug(
                "Attached HTTP content to {Method} serializableRequestMessage for {Uri}. ContentType={ContentType}, CorrelationId={CorrelationId}",
                method,
                uri,
                httpRequest.Content.Headers.ContentType?.MediaType ?? "<none>",
                correlationId);
        }

        if (serializableRequestMessage.Headers is not null)
        {
            logger.LogDebug(
                "Applying {HeaderCount} custom headers to {Method} serializableRequestMessage for {Uri} (CorrelationId={CorrelationId})",
                serializableRequestMessage.Headers.Count,
                method,
                uri,
                correlationId);

            foreach (var header in serializableRequestMessage.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    logger.LogDebug(
                        "Skipping header with empty key for {Method} serializableRequestMessage to {Uri} (CorrelationId={CorrelationId})",
                        method,
                        uri,
                        correlationId);
                    continue;
                }

                // Try adding to serializableRequestMessage headers; if it fails, try content headers if available.
                if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    if (httpRequest.Content is not null)
                    {
                        var addedToContent = httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        logger.LogDebug(
                            "Header '{HeaderKey}' could not be added to serializableRequestMessage headers for {Method} {Uri}. " +
                            "AddedToContentHeaders={AddedToContent}, CorrelationId={CorrelationId}",
                            header.Key,
                            method,
                            uri,
                            addedToContent,
                            correlationId);
                    }
                    else
                    {
                        logger.LogDebug(
                            "Header '{HeaderKey}' could not be added to serializableRequestMessage headers for {Method} {Uri} " +
                            "and no content is available to attach it to. CorrelationId={CorrelationId}",
                            header.Key,
                            method,
                            uri,
                            correlationId);
                    }
                }
                else
                {
                    logger.LogDebug(
                        "Added header '{HeaderKey}' to serializableRequestMessage headers for {Method} {Uri} (CorrelationId={CorrelationId})",
                        header.Key,
                        method,
                        uri,
                        correlationId);
                }
            }
        }

        var hasPerRequestToken = serializableRequestMessage.CancellationToken.CanBeCanceled;
        var hasTimeout = serializableRequestMessage.Timeout is { } timeoutValue && timeoutValue > TimeSpan.Zero;

        logger.LogDebug(
            "Building effective cancellation token for {Method} {Uri}. HasPerRequestToken={HasPerRequestToken}, HasTimeout={HasTimeout}, Timeout={Timeout}, CorrelationId={CorrelationId}",
            method,
            uri,
            hasPerRequestToken,
            hasTimeout,
            serializableRequestMessage.Timeout,
            correlationId);

        // Build effective cancellation token: outer, per-serializableRequestMessage and optional timeout.
        using var effectiveCancellationTokenSource = CreateEffectiveCancellationToken(serializableRequestMessage, cancellationToken, out var effectiveCancellationToken);

        HttpResponseMessage response;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // If you ever want to stream instead of buffer by default, you can switch to:
            // await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken)
            logger.LogDebug(
                "Dispatching HTTP {Method} serializableRequestMessage to {Uri}. Version={Version}, HasContent={HasContent}, RequestHeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
                method,
                uri,
                httpRequest.Version,
                httpRequest.Content is not null,
                httpRequest.Headers.Count(),
                correlationId);

            response = await httpClient
                .SendAsync(httpRequest, effectiveCancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            logger.LogDebug(
                "Received HTTP response for {Method} {Uri}. StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}, Version={Version}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                (int)response.StatusCode,
                response.ReasonPhrase,
                response.Version,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
        catch (OperationCanceledException oce) when (effectiveCancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                // Outer caller cancellation
                logger.LogDebug(
                    "HTTP {Method} serializableRequestMessage to {Uri} was cancelled by caller after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (serializableRequestMessage.CancellationToken.IsCancellationRequested)
            {
                // Per-serializableRequestMessage cancellation token
                logger.LogDebug(
                    "HTTP {Method} serializableRequestMessage to {Uri} was cancelled by per-serializableRequestMessage token after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (serializableRequestMessage.Timeout is { } timeout)
            {
                // Per-serializableRequestMessage timeout
                logger.LogWarning(
                    oce,
                    "HTTP {Method} serializableRequestMessage to {Uri} timed out after {Timeout} (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
                    method,
                    uri,
                    timeout,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else
            {
                // Fallback – should rarely hit if all cases above are covered
                logger.LogDebug(
                    oce,
                    "HTTP {Method} serializableRequestMessage to {Uri} was cancelled (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }

            throw;
        }
        catch (HttpRequestException httpException)
        {
            stopwatch.Stop();

            logger.LogWarning(
                httpException,
                "HTTP {Method} serializableRequestMessage failed while fetching resource from '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            // Return a synthetic response so callers can continue gracefully on DNS/connection failures.
            return new CompletedHttpResponseMessage
            {
                SerializableRequest = serializableRequestMessage,
                Method = serializableRequestMessage.Method.Method,
                Uri = uri,
                StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                Message = $"SerializableRequest failed: {httpException.Message}",
                Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                CorrelationId = correlationId
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            logger.LogWarning(
                exception,
                "Unexpected error occurred while sending {Method} serializableRequestMessage for resource '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);
            
            return new CompletedHttpResponseMessage
            {
                SerializableRequest = serializableRequestMessage,
                Method = serializableRequestMessage.Method.Method,
                Uri = uri,
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
                Message = $"Unexpected send error: {exception.Message}",
                Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                CorrelationId = correlationId
            };
        }

        var isHead = method == HttpMethod.Head;
        var readBodyOnSuccess = !isHead && (serializableRequestMessage.ReadBodyOnSuccess ?? settings.DefaultClient.ReadBodyOnSuccess ?? true);
        var readBodyOnNonSuccess = !isHead && (serializableRequestMessage.ReadBodyOnNonSuccess ?? settings.DefaultClient.ReadBodyOnNonSuccess ?? false);

        using (response)
        {
            logger.LogDebug(
                "Mapping HTTP response for {Method} {Uri}. StatusCode={StatusCode}, IsSuccess={IsSuccess}, ReadBodyOnSuccess={ReadBodyOnSuccess}, ReadBodyOnNonSuccess={ReadBodyOnNonSuccess}, CorrelationId={CorrelationId}",
                method,
                uri,
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                readBodyOnSuccess,
                readBodyOnNonSuccess,
                correlationId);

            var result = await responseMessageProcessor.ProcessAsync(
                serializableRequestMessage,
                response,
                correlationId, 
                uri, 
                readBodyOnSuccess, 
                readBodyOnNonSuccess, 
                serializableRequestMessage.ShouldReadBodyAsync, 
                effectiveCancellationToken)
            .ConfigureAwait(false);

            return result;

        }

    }
    
    private static CancellationTokenSource CreateEffectiveCancellationToken(
        SerializableRequestMessage serializableRequestMessage,
        CancellationToken outerCancellationToken,
        out CancellationToken effectiveCancellationToken)
    {
        // We always create a CancellationTokenSource to allow per-serializableRequestMessage timeout even if outerCancellationToken is None.
        var hasPerRequestToken = serializableRequestMessage.CancellationToken.CanBeCanceled;
        var hasTimeout = serializableRequestMessage.Timeout is { } timeout && timeout > TimeSpan.Zero;

        var cancellationTokenSource = hasPerRequestToken
            ? CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken, serializableRequestMessage.CancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken);

        if (hasTimeout)
        {
            cancellationTokenSource.CancelAfter(serializableRequestMessage.Timeout!.Value);
        }

        effectiveCancellationToken = cancellationTokenSource.Token;

        return cancellationTokenSource;
    }

    #endregion

}
