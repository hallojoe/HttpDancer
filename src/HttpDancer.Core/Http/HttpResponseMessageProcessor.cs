using HttpDancer.Core.Http.Clients;
using Microsoft.Extensions.Logging;

namespace HttpDancer.Core.Http;

public interface IHttpResponseMessageProcessor
{
    Task<CompletedHttpResponseMessage> ProcessAsync(
        RequestMessage requestMessage,
        HttpResponseMessage response,
        string correlationId,
        Uri originalUri,
        bool readBodyOnSuccess,
        bool readBodyOnNonSuccess,
        Func<HttpResponseMessage, Task<bool?>>? shouldReadBodyAsync,
        CancellationToken cancellationToken);
}

public static class HttpConstants
{
    public const string DefaultContentType = "application/octet-stream";
    public static readonly string[] MethodsWithNoResponseBody = ["HEAD", "TRACE", "CONNECT"];
}

public class HttpResponseMessageProcessor(ILogger<HttpResponseMessageProcessor> logger) : IHttpResponseMessageProcessor
{
    public async Task<CompletedHttpResponseMessage> ProcessAsync(
        RequestMessage requestMessage,
        HttpResponseMessage response,
        string correlationId,
        Uri originalUri,
        bool readBodyOnSuccess,
        bool readBodyOnNonSuccess,
        Func<HttpResponseMessage, Task<bool?>>? shouldReadBodyAsync,
        CancellationToken cancellationToken)
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
            "Building CompletedHttpResponseMessage for {Uri}. EffectiveUri={EffectiveUri}, StatusCode={StatusCode}, IsSuccess={IsSuccess}, ContentType={ContentType}, HeaderCount={HeaderCount}, CorrelationId={CorrelationId}",
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

        var responseMessage = new CompletedHttpResponseMessage
        {
            Request = requestMessage,
            Method = response.RequestMessage?.Method.Method,
            ContentType = contentType,
            StatusCode = statusCode,
            Uri = effectiveUri,
            Headers = headers,
            Message = message,
            BodyLength = response.Content.Headers.ContentLength ?? -1,
            CorrelationId = correlationId
        };
        
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
}
