using System.Text;
using HttpDancer.Core.Http.Clients;
using HttpDancer.Extensions;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.Scheduling.RatedScheduling;

namespace HttpDancer.Console.Workflows;

public class HttpFileFactory(
    IHttpClient httpClient, 
    IHttpFileParser httpFileParser)
{

    public HttpFileDocument Create(HttpFileDocument httpFileDocument, HttpFileDocument httpFileDocumentLog)
    {
        var variables = new Dictionary<string, string>(httpFileDocument.Variables);
        var variablesLog = new Dictionary<string, string>(httpFileDocumentLog.Variables);
        foreach (var variableLogKey in variablesLog.Keys)
        {
            if (!variables.TryAdd(variableLogKey, variablesLog[variableLogKey]))
            {
                variables[variableLogKey] = variablesLog[variableLogKey];
            }
        }

        var requests = new List<HttpRequestDefinition>(httpFileDocument.Requests);
        var requestsLog = new List<HttpRequestDefinition>(httpFileDocumentLog.Requests);

        foreach (var requestAppendix in requestsLog)
        {
            requests.RemoveAll(request => request.Url.ToString().Equals(requestAppendix.Url.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        return new HttpFileDocument(variables, requests);               
    }

    public HttpFileDocument Create(
        HttpFileDocument httpFileDocument,
        RatedSchedule ratedSchedule,
        DateTime startDate,
        Dictionary<int, RatedScheduleResponseMessage?> callbacks)
    {
        var requests = httpFileDocument.Requests.ToHashSet().ToList();

        var baseUrl = httpFileDocument.Variables.GetValueOrDefault("baseUrl", string.Empty);

        var variables = new Dictionary<string, string>(httpFileDocument.Variables);

        variables.TryAdd("baseUrl", baseUrl);
        variables.TryAdd("capacity", ratedSchedule.Capacity.ToString());
        variables.TryAdd("rate", ratedSchedule.Options?.Rate.ToString() ?? string.Empty);
        variables.TryAdd("rateWindow", ratedSchedule.Options?.RateWindow.ToString() ?? string.Empty);
        variables.TryAdd("minDuration", ratedSchedule.Options?.MinDuration.ToString() ?? string.Empty);
        variables.TryAdd("startDate", startDate.ToString("o"));        
        
        
        var httpRequestDefinitionList = new List<HttpRequestDefinition>();
        foreach (var request in requests)
        {
            var index = requests.IndexOf(request);
            if (callbacks.TryGetValue(index, out var ratedScheduleResponseMessage) is not true ||
                ratedScheduleResponseMessage?.ResponseMessage is null)
            {
                continue;                
            }
            
            var httpRequestDefinitionHeaders = request.Headers.Concat(CreateHttpFileDocumentHeaders(request, ratedSchedule.Offsets[index],
                ratedScheduleResponseMessage))
                .ToList();
            
            var httpRequestDefinition = new HttpRequestDefinition(
                request.Name, 
                request.Method, 
                request.Url, 
                httpRequestDefinitionHeaders, 
                null);
            
            httpRequestDefinitionList.Add(httpRequestDefinition);
            
        }
        
        return new HttpFileDocument(variables, httpRequestDefinitionList);
    }
    
    private IReadOnlyList<HttpHeader> CreateHttpFileDocumentHeaders(
        HttpRequestDefinition request, 
        TimeSpan ratedScheduleTimeSpan, 
        RatedScheduleResponseMessage ratedScheduleResponseMessage)
    {
        var httpRequestDefinitionHeaders = new List<HttpHeader>(request.Headers);        
        
        var statusCode = (int)(ratedScheduleResponseMessage.ResponseMessage?.StatusCode ?? 0);

        httpRequestDefinitionHeaders.Add(new HttpHeader("X-Request-Offset", ratedScheduleTimeSpan.ToString()));
        httpRequestDefinitionHeaders.Add(new HttpHeader("X-Response-Planned", ratedScheduleResponseMessage.PlannedDateTime.ToString("o")));
        httpRequestDefinitionHeaders.Add(new HttpHeader("X-Response-Executed", ratedScheduleResponseMessage.ExecutionDateTime?.ToString("o") ?? string.Empty));
        httpRequestDefinitionHeaders.Add(new HttpHeader("X-Response-Status", statusCode.ToString()));

        if (statusCode is >= 0 and < 200 or >= 400 and < 600 && string.IsNullOrWhiteSpace(ratedScheduleResponseMessage.ResponseMessage?.Message) is false)
        {
            httpRequestDefinitionHeaders.Add(
                new HttpHeader("X-Response-Message", ratedScheduleResponseMessage.ResponseMessage.Message));
        }

        if (statusCode is < 300 or >= 400) return httpRequestDefinitionHeaders;
        
        var headers = ratedScheduleResponseMessage.ResponseMessage?.Headers;

        if (headers is null || !headers.TryGetValue("Location", out var values)) return httpRequestDefinitionHeaders;
        
        var location = values.FirstOrDefault()?.Trim();
        
        if (!string.IsNullOrWhiteSpace(location))
        {
            httpRequestDefinitionHeaders.Add(
                new HttpHeader("X-Response-Location", location)
            );
        }

        return httpRequestDefinitionHeaders;
    }
    
    /// <summary>
    /// Creates an instance of <see cref="HttpFileDocument"/> by making an HTTP request to the specified URI,
    /// validating the response, and parsing it into '.http' format.
    /// </summary>
    /// <param name="uri">The URI to which the HTTP request will be made.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="HttpFileDocument"/> that represents the parsed content from the response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the response body is null or the content type is not supported.</exception>
    public async Task<HttpFileDocument> CreateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(new SerializableRequestMessage() { Uri = uri, Method = HttpMethod.Get}, cancellationToken);
        if (response.BodyBytes is null || response.ContentType?.IsMatch("text/*, application/*") is not true)
        {
            throw new InvalidOperationException("Response body is not textual content.");
        }
        var utf8EncodedStringWithUrls = Encoding.UTF8.GetString(response.BodyBytes);
        var httpFileDocument = httpFileParser.Parse(
            utf8EncodedStringWithUrls, 
            $"{uri.Scheme}://{uri.Host}", 
            "GET", 
            new HttpFileParseOptions(true, true));
        
        return httpFileDocument;
    }

    /// <summary>
    /// Creates an instance of <see cref="HttpFileDocument"/> by parsing the provided UTF-8 encoded '.http' file content.
    /// </summary>
    /// <param name="utf8EncodedHttpFileString">The content of the '.http' file encoded as a UTF-8 string.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="HttpFileDocument"/> representing the parsed data from the provided content.</returns>
    public HttpFileDocument Create(string utf8EncodedHttpFileString, CancellationToken cancellationToken = default)
    {
        var httpFileDocument = httpFileParser.Parse(
            utf8EncodedHttpFileString, 
            new HttpFileParseOptions(true, true));
        return httpFileDocument;
    }

    /// <summary>
    /// Creates an instance of <see cref="HttpFileDocument"/> by parsing a UTF-8 encoded HTTP file string,
    /// associating it with the specified base URL and HTTP method, and resolving any URLs provided in the collection.
    /// </summary>
    /// <param name="baseUrl">The base URL to be used as the root for relative URLs in the HTTP file.</param>
    /// <param name="method">The HTTP method associated with the requests in the parsed HTTP file.</param>
    /// <param name="urlCollection">An array of URLs to be included in the HTTP file document.</param>
    /// <returns>A <see cref="HttpFileDocument"/> representing the parsed HTTP file content.</returns>
    public HttpFileDocument Create(string baseUrl, string method, string[] urlCollection)
    {
        var utf8EncodedStringWithUrls = string.Join(Environment.NewLine, urlCollection);
        var httpFileDocument = 
            httpFileParser.Parse(
                utf8EncodedStringWithUrls, 
                baseUrl, 
                method, 
                new HttpFileParseOptions(true, true));
        return httpFileDocument;
    }
}