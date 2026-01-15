// using System.Diagnostics;
// using System.Net.Http.Headers;
// using HttpDancer.Core.Http.Observability;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace HttpDancer.Core.Http.Clients;
//
// public interface IHttpClientV2
// {
//     Task<CompletedHttpResponseMessage> SendAsync(
//         HttpRequestMessage httpRequestMessage, 
//         HttpPipelineOptions httpPipelineOptions, 
//         CancellationToken? cancellationToken);
// }
//
// public sealed class HttpPipelineOptions
// {
//     public bool ReadBodyOnSuccess { get; set; } = true;
//     public bool ReadBodyOnNonSuccess { get; set; } = false;
//     public Func<HttpResponseMessage, Task<bool?>>? ShouldProcessResponseAsync { get; init; }
//     public Func<HttpResponseMessage, Task<bool?>>? ProcessResponseAsync { get; init; }
// }
//
// public class DefaultHttpClient2(
//     ILogger<DefaultHttpClient> logger,
//     IOptionsMonitor<HttpDancerSettings> apiClientSettings,
//     ICorrelationIdProvider correlationIdProvider, 
//     IHttpResponseMessageProcessorV2 responseMessageProcessor,
//     HttpClient httpClient) : IHttpClientV2
// {
//     public async Task<CompletedHttpResponseMessage> SendAsync(
//         HttpRequestMessage httpRequestMessage, 
//         HttpPipelineOptions httpPipelineOptions, 
//         CancellationToken? cancellationToken)
//     {
//         var correlationId = correlationIdProvider.GetCorrelationId();
//         var settings = apiClientSettings.CurrentValue;
//
//         if (string.IsNullOrWhiteSpace(correlationId) is false)
//         {
//             httpRequestMessage.Headers.TryAddWithoutValidation("X-CorrelationId", correlationId);
//         }
//         
//         try
//         { 
//             using var httpResponseMessage = await httpClient
//                 .SendAsync(httpRequestMessage, cancellationToken ?? CancellationToken.None)
//                 .ConfigureAwait(false);
//         
//             var expectBody = !HttpConstants.MethodsWithoutExpectedResponseBody.Contains(httpRequestMessage.Method.ToString());
//             var readBodyOnSuccess = expectBody && httpPipelineOptions.ReadBodyOnSuccess;
//             var readBodyOnNonSuccess = expectBody && httpPipelineOptions.ReadBodyOnNonSuccess;
//
//             return await responseMessageProcessor.ProcessAsync(
//                     httpResponseMessage,
//                     httpPipelineOptions, 
//                     cancellationToken)
//                 .ConfigureAwait(false);
//             
//         }
//         catch (Exception exception)
//         {
//                 //"Unexpected error occurred while sending {Method} serializableRequestMessage for resource '{Uri}'. ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
//         }
//
//     }
//     
//     
// }
