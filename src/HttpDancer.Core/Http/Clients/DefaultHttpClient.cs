using System.Diagnostics;
using HttpDancer.Core.Configuration;
using HttpDancer.Core.Http.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Http.Clients;

public class DefaultHttpClient(
    ILogger<DefaultHttpClient> logger,
    IOptionsMonitor<HttpDancerSettings> apiClientSettings,
    ICorrelationIdProvider correlationIdProvider, 
    HttpClient httpClient) : IHttpClient
{
    #region Convenience methods

    public Task<ResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
        => GetAsync(new Uri(url), cancellationToken);

    public Task<ResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
        => SendAsync(
            new RequestMessage
            {
                Uri = uri,
                Method = HttpMethod.Get
            },
            cancellationToken);

    public Task<ResponseMessage> PostAsync(string url, HttpContent? content, CancellationToken cancellationToken)
        => PostAsync(new Uri(url), content, cancellationToken);

    public Task<ResponseMessage> PostAsync(Uri uri, HttpContent? content, CancellationToken cancellationToken)
        => SendAsync(
            new RequestMessage
            {
                Uri = uri,
                Method = HttpMethod.Post,
                Content = content
            },
            cancellationToken);

    public Task<ResponseMessage> HeadAsync(string url, CancellationToken cancellationToken)
        => HeadAsync(new Uri(url), cancellationToken);

    public Task<ResponseMessage> HeadAsync(Uri uri, CancellationToken cancellationToken)
        => SendAsync(
            new RequestMessage
            {
                Uri = uri,
                Method = HttpMethod.Head
            },
            cancellationToken);

    #endregion

    #region Public generic entrypoint

    public Task<ResponseMessage> SendAsync(RequestMessage requestMessage, CancellationToken cancellationToken)
        => SendInternalAsync(requestMessage, cancellationToken);

    #endregion

    #region Core Send Logic

    private async Task<ResponseMessage> SendInternalAsync(
        RequestMessage requestMessage,
        CancellationToken cancellationToken)
    {
        var uri = requestMessage.Uri ?? throw new ArgumentNullException(nameof(requestMessage.Uri));
        var method = requestMessage.Method ?? throw new ArgumentNullException(nameof(requestMessage.Method));

        var correlationId = correlationIdProvider.GetCorrelationId();

        logger.LogInformation(
            "Sending {Method} requestMessage to {Uri} (CorrelationId={CorrelationId})",
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

        if (requestMessage.Content is not null)
        {
            httpRequest.Content = requestMessage.Content;
            logger.LogDebug(
                "Attached HTTP content to {Method} requestMessage for {Uri}. ContentType={ContentType}, CorrelationId={CorrelationId}",
                method,
                uri,
                httpRequest.Content.Headers.ContentType?.MediaType ?? "<none>",
                correlationId);
        }

        if (requestMessage.Headers is not null)
        {
            logger.LogDebug(
                "Applying {HeaderCount} custom headers to {Method} requestMessage for {Uri} (CorrelationId={CorrelationId})",
                requestMessage.Headers.Count,
                method,
                uri,
                correlationId);

            foreach (var header in requestMessage.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    logger.LogDebug(
                        "Skipping header with empty key for {Method} requestMessage to {Uri} (CorrelationId={CorrelationId})",
                        method,
                        uri,
                        correlationId);
                    continue;
                }

                // Try adding to requestMessage headers; if it fails, try content headers if available.
                if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    if (httpRequest.Content is not null)
                    {
                        var addedToContent = httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        logger.LogDebug(
                            "Header '{HeaderKey}' could not be added to requestMessage headers for {Method} {Uri}. " +
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
                            "Header '{HeaderKey}' could not be added to requestMessage headers for {Method} {Uri} " +
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
                        "Added header '{HeaderKey}' to requestMessage headers for {Method} {Uri} (CorrelationId={CorrelationId})",
                        header.Key,
                        method,
                        uri,
                        correlationId);
                }
            }
        }

        var hasPerRequestToken = requestMessage.CancellationToken.CanBeCanceled;
        var hasTimeout = requestMessage.Timeout is { } timeoutValue && timeoutValue > TimeSpan.Zero;

        logger.LogDebug(
            "Building effective cancellation token for {Method} {Uri}. HasPerRequestToken={HasPerRequestToken}, HasTimeout={HasTimeout}, Timeout={Timeout}, CorrelationId={CorrelationId}",
            method,
            uri,
            hasPerRequestToken,
            hasTimeout,
            requestMessage.Timeout,
            correlationId);

        // Build effective cancellation token: outer, per-requestMessage and optional timeout.
        using var effectiveCancellationTokenSource = CreateEffectiveCancellationToken(requestMessage, cancellationToken, out var effectiveCancellationToken);

        HttpResponseMessage response;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // If you ever want to stream instead of buffer by default, you can switch to:
            // await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken)
            logger.LogDebug(
                "Dispatching HTTP {Method} requestMessage to {Uri}. Version={Version}, HasContent={HasContent}, RequestHeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
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
                    "HTTP {Method} requestMessage to {Uri} was cancelled by caller after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (requestMessage.CancellationToken.IsCancellationRequested)
            {
                // Per-requestMessage cancellation token
                logger.LogInformation(
                    "HTTP {Method} requestMessage to {Uri} was cancelled by per-requestMessage token after {ElapsedMs} ms. CorrelationId={CorrelationId}",
                    method,
                    uri,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
            }
            else if (requestMessage.Timeout is { } timeout)
            {
                // Per-requestMessage timeout
                logger.LogWarning(
                    oce,
                    "HTTP {Method} requestMessage to {Uri} timed out after {Timeout} (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
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
                    "HTTP {Method} requestMessage to {Uri} was cancelled (ElapsedMs={ElapsedMs}). CorrelationId={CorrelationId}",
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
                "HTTP {Method} requestMessage failed while fetching resource from '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);

            // Return a synthetic response so callers can continue gracefully on DNS/connection failures.
            return new ResponseMessage
            {
                Uri = uri,
                StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                Message = httpException.Message,
                Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                CorrelationId = correlationId
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            logger.LogWarning(
                exception,
                "Unexpected error occurred while sending {Method} requestMessage for resource '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                method,
                uri,
                stopwatch.ElapsedMilliseconds,
                correlationId);
            
            return new ResponseMessage
            {
                Uri = uri,
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
                Message = exception.Message,
                Headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                CorrelationId = correlationId
            };
        }

        using (response)
        {
            var isHead = method == HttpMethod.Head;

            var readBodyOnSuccess = !isHead && (requestMessage.ReadBodyOnSuccess ?? settings.ReadBodyOnSuccess ?? true);
            var readBodyOnNonSuccess = !isHead && (requestMessage.ReadBodyOnNonSuccess ?? settings.ReadBodyOnNonSuccess ?? false);

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
                    requestMessage.ShouldReadBodyAsync,
                    effectiveCancellationToken,
                    correlationId)
                .ConfigureAwait(false);
        }
    }

    private static CancellationTokenSource CreateEffectiveCancellationToken(
        RequestMessage requestMessage,
        CancellationToken outerCancellationToken,
        out CancellationToken effectiveCancellationToken)
    {
        // We always create a CancellationTokenSource to allow per-requestMessage timeout even if outerCancellationToken is None.
        var hasPerRequestToken = requestMessage.CancellationToken.CanBeCanceled;
        var hasTimeout = requestMessage.Timeout is { } timeout && timeout > TimeSpan.Zero;

        var cancellationTokenSource = hasPerRequestToken
            ? CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken, requestMessage.CancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(outerCancellationToken);

        if (hasTimeout)
        {
            cancellationTokenSource.CancelAfter(requestMessage.Timeout!.Value);
        }

        effectiveCancellationToken = cancellationTokenSource.Token;

        return cancellationTokenSource;
    }

    #endregion

    #region Value Mapping

    private async Task<ResponseMessage> BuildResourceResponseAsync(
        HttpResponseMessage response,
        Uri originalUri,
        bool readBodyOnSuccess,
        bool readBodyOnNonSuccess,
        Func<ResponseMessage, Task<bool?>>? shouldReadBodyAsync,
        CancellationToken cancellationToken,
        string correlationId)
    {
        var effectiveUri = response.RequestMessage?.RequestUri ?? originalUri;
        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var statusCode = response.StatusCode;
        var headers = GetHeaders(response);
        var isSuccess = response.IsSuccessStatusCode;
        var message = isSuccess
            ? null
            : $"Endpoint responded with status {statusCode} {response.ReasonPhrase} when requesting resource {effectiveUri}";

        logger.LogDebug(
            "Building ResponseMessage for {Uri}. EffectiveUri={EffectiveUri}, StatusCode={StatusCode}, IsSuccess={IsSuccess}, ContentType={ContentType}, HeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
            originalUri,
            effectiveUri,
            (int)statusCode,
            isSuccess,
            string.IsNullOrWhiteSpace(contentType) ? "<none>" : contentType,
            headers.Count,
            correlationId);

        if (effectiveUri != originalUri)
        {
            logger.LogDebug(
            "Effective URI differs from original. Original={OriginalUri}, Effective={EffectiveUri}, CorrelationId={CorrelationId}",
            originalUri,
            effectiveUri,
            correlationId);
        }

        var responseMessage = new ResponseMessage
        {
            ContentType = contentType,
            StatusCode = statusCode,
            Uri = effectiveUri,
            Headers = headers,
            Message = message,
            CorrelationId = correlationId
        };

        bool? shouldReadOverride = null;
        if (shouldReadBodyAsync is not null)
        {
            try
            {
                shouldReadOverride = await shouldReadBodyAsync(responseMessage).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "ShouldReadBodyAsync failed for response from {Uri}. Continuing with defaults. CorrelationId={CorrelationId}",
                    effectiveUri,
                    correlationId);
            }
        }

        if (shouldReadOverride.HasValue)
        {
            readBodyOnSuccess = shouldReadOverride.Value;
            readBodyOnNonSuccess = shouldReadOverride.Value;

            logger.LogDebug(
                "Body read override applied for {Uri}. Override={Override}, CorrelationId={CorrelationId}",
                effectiveUri,
                shouldReadOverride,
                correlationId);
        }

        if (isSuccess)
        {
            if (readBodyOnSuccess)
            {
                try
                {
                    var bodyBytes = await response.Content
                        .ReadAsByteArrayAsync(cancellationToken)
                        .ConfigureAwait(false);

                    logger.LogDebug(
                        "Read success response body for {Uri}. Length={Length} bytes, CorrelationId={CorrelationId}",
                        effectiveUri,
                        bodyBytes.Length,
                        correlationId);

                    responseMessage.BodyBytes = bodyBytes;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Failed to read success body for {Uri}. CorrelationId={CorrelationId}",
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

            return responseMessage;
        }

        logger.LogWarning(
            "Non-success status received: {Message} (StatusCode: {StatusCode}, CorrelationId={CorrelationId})",
            message,
            (int)statusCode,
            correlationId);

        byte[]? errorBody = null;

        if (readBodyOnNonSuccess)
        {
            try
            {
                errorBody = await response.Content
                    .ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                logger.LogDebug(
                    "Read error response body for {Uri}. Length={Length} bytes, CorrelationId={CorrelationId}",
                    effectiveUri,
                    errorBody.Length,
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

        responseMessage.BodyBytes = errorBody;
        return responseMessage;
    }

    private static Dictionary<string, IReadOnlyList<string>> GetHeaders(HttpResponseMessage response)
    {
        // Use case-insensitive key comparer (required for HTTP semantics)
        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        // Add response headers
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToList().AsReadOnly();
        }

        // Add content headers (if any)
        foreach (var header in response.Content.Headers)
        {
            if (headers.TryGetValue(header.Key, out var existing))
            {
                // Merge values in case the header exists in both collections
                var merged = existing.Concat(header.Value).ToList().AsReadOnly();
                headers[header.Key] = merged;
            }
            else
            {
                headers[header.Key] = header.Value.ToList().AsReadOnly();
            }
        }

        return headers;
    }
    
    #endregion
}
