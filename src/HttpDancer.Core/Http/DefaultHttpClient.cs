using System.Diagnostics;
using HttpDancer.Core.Configuration;
using HttpDancer.Core.Extensions;
using HttpDancer.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Http;

public class DefaultHttpClient(
    ILogger<DefaultHttpClient> logger,
    IOptionsMonitor<ApiClientSettings> apiClientSettings,
    ICorrelationIdProvider correlationIdProvider, 
    HttpClient httpClient) : IHttpClient
{
    // NOTE: apiClientSettings is currently used for ReadBodyOnSuccess/ReadBodyOnNonSuccess and correlation header config (via handler).

    #region Convenience methods

    public Task<ResourceResponse> GetAsync(string url, CancellationToken cancellationToken)
        => GetAsync(new Uri(url), cancellationToken);

    public Task<ResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken)
        => SendAsync(
            new ResourceRequest
            {
                Uri = uri,
                Method = HttpMethod.Get
            },
            cancellationToken);

    public Task<ResourceResponse> PostAsync(string url, HttpContent? content, CancellationToken cancellationToken)
        => PostAsync(new Uri(url), content, cancellationToken);

    public Task<ResourceResponse> PostAsync(Uri uri, HttpContent? content, CancellationToken cancellationToken)
        => SendAsync(
            new ResourceRequest
            {
                Uri = uri,
                Method = HttpMethod.Post,
                Content = content
            },
            cancellationToken);

    public Task<ResourceResponse> HeadAsync(string url, CancellationToken cancellationToken)
        => HeadAsync(new Uri(url), cancellationToken);

    public Task<ResourceResponse> HeadAsync(Uri uri, CancellationToken cancellationToken)
        => SendAsync(
            new ResourceRequest
            {
                Uri = uri,
                Method = HttpMethod.Head
            },
            cancellationToken);

    #endregion

    #region Public generic entrypoint

    public Task<ResourceResponse> SendAsync(ResourceRequest request, CancellationToken cancellationToken)
        => SendInternalAsync(request, cancellationToken);

    #endregion

    #region Core Send Logic

    private async Task<ResourceResponse> SendInternalAsync(
        ResourceRequest request,
        CancellationToken cancellationToken)
    {
        var uri = request.Uri ?? throw new ArgumentNullException(nameof(request.Uri));
        var method = request.Method ?? throw new ArgumentNullException(nameof(request.Method));

        var correlationId = correlationIdProvider.GetCorrelationId();

        logger.LogInformation(
            "Sending {Method} request to {Uri} (CorrelationId={CorrelationId})",
            method,
            uri,
            correlationId);

        var settings = apiClientSettings.CurrentValue;

        logger.LogDebug(
            "ApiClient settings snapshot for {Method} {Uri} (CorrelationId={CorrelationId}): ReadBodyOnSuccess={ReadBodyOnSuccess}, ReadBodyOnNonSuccess={ReadBodyOnNonSuccess}",
            method,
            uri,
            correlationId,
            settings.ReadBodyOnSuccess,
            settings.ReadBodyOnNonSuccess);

        using var httpRequest = new HttpRequestMessage(method, uri);

        if (request.Content is not null)
        {
            httpRequest.Content = request.Content;
            logger.LogDebug(
                "Attached HTTP content to {Method} request for {Uri}. ContentType={ContentType}, CorrelationId={CorrelationId}",
                method,
                uri,
                httpRequest.Content.Headers.ContentType?.MediaType ?? "<none>",
                correlationId);
        }

        if (request.Headers is not null)
        {
            logger.LogDebug(
                "Applying {HeaderCount} custom headers to {Method} request for {Uri} (CorrelationId={CorrelationId})",
                request.Headers.Count,
                method,
                uri,
                correlationId);

            foreach (var header in request.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    logger.LogDebug(
                        "Skipping header with empty key for {Method} request to {Uri} (CorrelationId={CorrelationId})",
                        method,
                        uri,
                        correlationId);
                    continue;
                }

                // Try adding to request headers; if it fails, try content headers if available.
                if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    if (httpRequest.Content is not null)
                    {
                        var addedToContent = httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        logger.LogDebug(
                            "Header '{HeaderKey}' could not be added to request headers for {Method} {Uri}. " +
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
                            "Header '{HeaderKey}' could not be added to request headers for {Method} {Uri} " +
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
                        "Added header '{HeaderKey}' to request headers for {Method} {Uri} (CorrelationId={CorrelationId})",
                        header.Key,
                        method,
                        uri,
                        correlationId);
                }
            }
        }

        var hasPerRequestToken = request.CancellationToken.CanBeCanceled;
        var hasTimeout = request.Timeout is { } timeoutValue && timeoutValue > TimeSpan.Zero;

        logger.LogDebug(
            "Building effective cancellation token for {Method} {Uri}. HasPerRequestToken={HasPerRequestToken}, HasTimeout={HasTimeout}, Timeout={Timeout}, CorrelationId={CorrelationId}",
            method,
            uri,
            hasPerRequestToken,
            hasTimeout,
            request.Timeout,
            correlationId);

        // Build effective cancellation token: outer, per-request and optional timeout.
        using var effectiveCancellationTokenSource = CreateEffectiveCancellationToken(request, cancellationToken, out var effectiveCancellationToken);

        HttpResponseMessage response;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // If you ever want to stream instead of buffer by default, you can switch to:
            // await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken)
            logger.LogDebug(
                "Dispatching HTTP {Method} request to {Uri}. Version={Version}, HasContent={HasContent}, RequestHeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
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
                logger.LogInformation(
                    "HTTP {Method} request to {Uri} was cancelled by caller after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (request.CancellationToken.IsCancellationRequested)
            {
                // Per-request cancellation token
                logger.LogInformation(
                    "HTTP {Method} request to {Uri} was cancelled by per-request token after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (request.Timeout is { } timeout)
            {
                // Per-request timeout
                logger.LogWarning(
                    oce,
                    "HTTP {Method} request to {Uri} timed out after {Timeout} (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
                    method,
                    uri,
                    timeout,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else
            {
                // Fallback – should rarely hit if all cases above are covered
                logger.LogInformation(
                    oce,
                    "HTTP {Method} request to {Uri} was cancelled (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
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

            logger.LogError(
                httpException,
                "HTTP {Method} request failed while fetching resource from '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            logger.LogError(
                exception,
                "Unexpected error occurred while sending {Method} request for resource '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);
            throw;
        }

        using (response)
        {
            var isHead = method == HttpMethod.Head;

            var readBodyOnSuccess = !isHead && (request.ReadBodyOnSuccess ?? settings.ReadBodyOnSuccess ?? true);
            var readBodyOnNonSuccess = !isHead && (request.ReadBodyOnNonSuccess ?? settings.ReadBodyOnNonSuccess ?? false);

            logger.LogDebug(
                "Mapping HTTP response for {Method} {Uri}. StatusCode={StatusCode}, IsSuccess={IsSuccess}, ReadBodyOnSuccess={ReadBodyOnSuccess}, ReadBodyOnNonSuccess={ReadBodyOnNonSuccess}, CorrelationId={CorrelationId}",
                method,
                uri,
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                readBodyOnSuccess,
                readBodyOnNonSuccess,
                correlationId);

            return await BuildResourceResponseAsync(
                    response,
                    uri,
                    readBodyOnSuccess,
                    readBodyOnNonSuccess,
                    effectiveCancellationToken,
                    correlationId)
                .ConfigureAwait(false);
        }
    }

    private static CancellationTokenSource CreateEffectiveCancellationToken(
        ResourceRequest request,
        CancellationToken outerCancellationToken,
        out CancellationToken effectiveCancellationToken)
    {
        // We always create a CancellationTokenSource to allow per-request timeout even if outerCancellationToken is None.
        var hasPerRequestToken = request.CancellationToken.CanBeCanceled;
        var hasTimeout = request.Timeout is { } timeout && timeout > TimeSpan.Zero;

        var cancellationTokenSource = hasPerRequestToken
            ? CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken, request.CancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken);

        if (hasTimeout)
        {
            cancellationTokenSource.CancelAfter(request.Timeout!.Value);
        }

        effectiveCancellationToken = cancellationTokenSource.Token;

        return cancellationTokenSource;
    }

    #endregion

    #region Value Mapping

    private async Task<ResourceResponse> BuildResourceResponseAsync(
        HttpResponseMessage response,
        Uri originalUri,
        bool readBodyOnSuccess,
        bool readBodyOnNonSuccess,
        CancellationToken cancellationToken,
        string correlationId)
    {
        var effectiveUri = response.RequestMessage?.RequestUri ?? originalUri;
        var contentType = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;
        var statusCode = response.StatusCode;
        var headers = response.GetHeaders();
        var isSuccess = response.IsSuccessStatusCode;

        logger.LogDebug(
            "Building ResourceResponse for {Uri}. EffectiveUri={EffectiveUri}, StatusCode={StatusCode}, IsSuccess={IsSuccess}, ContentType={ContentType}, HeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
            originalUri,
            effectiveUri,
            (int)statusCode,
            isSuccess,
            string.IsNullOrWhiteSpace(contentType) ? "<none>" : contentType,
            headers?.Count ?? 0,
            correlationId);

        if (effectiveUri != originalUri)
        {
            logger.LogDebug(
                "Effective URI differs from original. Original={OriginalUri}, Effective={EffectiveUri}, CorrelationId={CorrelationId}",
                originalUri,
                effectiveUri,
                correlationId);
        }

        if (isSuccess)
        {
            byte[]? bodyBytes = null;

            if (readBodyOnSuccess)
            {
                if (response.Content is not null)
                {
                    bodyBytes = await response.Content
                        .ReadAsByteArrayAsync(cancellationToken)
                        .ConfigureAwait(false);

                    logger.LogDebug(
                        "Read success response body for {Uri}. Length={Length} bytes, CorrelationId={CorrelationId}",
                        effectiveUri,
                        bodyBytes?.Length ?? 0,
                        correlationId);
                }
                else
                {
                    logger.LogDebug(
                        "Success response for {Uri} has no content to read. CorrelationId={CorrelationId}",
                        effectiveUri,
                        correlationId);
                }
            }
            else
            {
                logger.LogDebug(
                    "Configured not to read body on success for {Uri}. CorrelationId={CorrelationId}",
                    effectiveUri,
                    correlationId);
            }

            return new ResourceResponse
            {
                ContentType = contentType,
                StatusCode = statusCode,
                Uri = effectiveUri,
                Headers = headers,
                BodyBytes = bodyBytes,
                CorrelationId = correlationId
            };
        }

        var message =
            $"Endpoint responded with status {statusCode} {response.ReasonPhrase} when requesting resource {effectiveUri}";

        logger.LogWarning(
            "Non-success status received: {Message} (StatusCode: {StatusCode}, CorrelationId={CorrelationId})",
            message,
            (int)statusCode,
            correlationId);

        byte[]? errorBody = null;

        if (readBodyOnNonSuccess && response.Content is not null)
        {
            try
            {
                errorBody = await response.Content
                    .ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                logger.LogDebug(
                    "Read error response body for {Uri}. Length={Length} bytes, CorrelationId={CorrelationId}",
                    effectiveUri,
                    errorBody?.Length ?? 0,
                    correlationId);
            }
            catch (Exception exception)
            {
                // Don't let body-read failures hide the original HTTP status.
                logger.LogDebug(
                    exception,
                    "Failed to read error body for response from {Uri}. CorrelationId={CorrelationId}",
                    effectiveUri,
                    correlationId);
            }
        }
        else if (readBodyOnNonSuccess)
        {
            logger.LogDebug(
                "Configured to read body on non-success for {Uri}, but response has no content. CorrelationId={CorrelationId}",
                effectiveUri,
                correlationId);
        }
        else
        {
            logger.LogDebug(
                "Configured not to read body on non-success for {Uri}. CorrelationId={CorrelationId}",
                effectiveUri,
                correlationId);
        }

        return new ResourceResponse
        {
            ContentType = contentType,
            StatusCode = statusCode,
            Uri = effectiveUri,
            Headers = headers,
            BodyBytes = errorBody,
            Message = message,
            CorrelationId = correlationId
        };
    }

    #endregion
}
