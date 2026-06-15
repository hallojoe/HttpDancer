using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Gr8Io.Threading.Tasks;
using Gr8Io.Threading.Tasks.Dataflow;
using HttpDancer.Core.Http.Clients;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.KnownMediaTypes;
using HttpDancer.Naming;
using HttpDancer.Scheduling.RatedScheduling;

namespace HttpDancer.Console.Workflows;

public record RatedScheduleResponseMessage(
    int Index, 
    DateTime PlannedDateTime, 
    DateTime? ExecutionDateTime, 
    CompletedHttpResponseMessage? ResponseMessage);

public sealed class FileSystemHttpFileProvider(IKnowMediaTypes knowMediaTypes, IUrlNamer urlNamer, IHttpFileParser httpFileParser, IHttpFileRenderer httpFileRenderer, HttpFileFactory httpFileFactory)
{

    private string BasePath = "c:\temp\ttt";
  
    public Task WriteAsync(CompletedHttpResponseMessage completedHttpResponseMessage, CancellationToken cancellationToken = default)
    {
        if(completedHttpResponseMessage.Uri is null)
        {
            throw new ArgumentNullException(nameof(completedHttpResponseMessage.Uri));
        }

        var nameAndPath = urlNamer.GetNameAndPath(completedHttpResponseMessage.Uri);
        var extension = knowMediaTypes.GetExtension(completedHttpResponseMessage.ContentType);
        var fileNameWithExtensionAndPath = $"{nameAndPath.FullPath}.{extension}";

        var json = CreateJson(completedHttpResponseMessage);
        if (string.IsNullOrWhiteSpace(json) is false)
        {
            // Persist http response 
            File.WriteAllTextAsync(Path.Join(BasePath, $"{fileNameWithExtensionAndPath}.meta.json"), json, cancellationToken)
                .SafeFireAndForget();
        }

        if (completedHttpResponseMessage.BodyBytes is not null)
        {
            // Persist http response body
            File.WriteAllBytesAsync(Path.Join(BasePath, fileNameWithExtensionAndPath), completedHttpResponseMessage.BodyBytes, cancellationToken)
                .SafeFireAndForget();
        }

        return Task.CompletedTask;
    }

    private static string? CreateJson(CompletedHttpResponseMessage completedHttpResponseMessage)
    {
        try
        {
            var serializedCompletedHttpResponseMessage = JsonSerializer.Serialize(completedHttpResponseMessage, new JsonSerializerOptions { WriteIndented = true });
            return serializedCompletedHttpResponseMessage;
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public async Task WriteAsync(string id, HttpFileDocument httpFileDocument, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        
        var processedHttpFileDocumentString = httpFileRenderer.Render(
            httpFileDocument,
            new HttpFileRenderOptions(true, true));

        await File.WriteAllTextAsync(
            Path.Combine(BasePath, id),
            processedHttpFileDocumentString,
            Encoding.UTF8, cancellationToken);
    }

    public async Task WriteAsync(string id, IReadOnlyList<RatedScheduleResponseMessage> ratedScheduleResponseMessages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        var hasExistingHttpFileDocument = Exist(id);
        if (!hasExistingHttpFileDocument)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(BasePath, id)) ?? string.Empty);
        }

        var existingHttpFileDocument = await ReadAsync(id, cancellationToken);

        var httpRequestDefinitionList = new List<HttpRequestDefinition>();
        
        foreach (var ratedScheduleResponseMessage in ratedScheduleResponseMessages)
        {
            if (string.IsNullOrWhiteSpace(ratedScheduleResponseMessage.ResponseMessage?.Uri?.ToString()))
            {
                continue;   
            }

            var httpHeaders = new List<HttpHeader>
            {
                new("Content-Type", ratedScheduleResponseMessage.ResponseMessage.ContentType ?? string.Empty),
                new("Content-Length",
                    ratedScheduleResponseMessage.ResponseMessage.BodyLength?.ToString() ?? string.Empty),
                new("X-CorrelationId", ratedScheduleResponseMessage.ResponseMessage.CorrelationId ?? string.Empty),

                new("Log:CompletionDateTime",
                    ratedScheduleResponseMessage.ResponseMessage.CompletionDateTime.ToString("o") ?? string.Empty),
                new("Log:ExecutionDateTime",
                    ratedScheduleResponseMessage.ExecutionDateTime?.ToString("o") ?? string.Empty),
                new("Log:PlannedDateTime", ratedScheduleResponseMessage.PlannedDateTime.ToString("o") ?? string.Empty),
            };

            if (ratedScheduleResponseMessage.ResponseMessage.Headers is not null && 
                ratedScheduleResponseMessage.ResponseMessage.Headers.TryGetValue("Location", out var locations))
            {
                httpHeaders.Insert(2, new("Location", locations.FirstOrDefault() ?? string.Empty));                
            }

            var httpRequestDefinition = new HttpRequestDefinition(
                "",
                new HttpMethod(ratedScheduleResponseMessage.ResponseMessage.Method ?? "HEAD"),
                ratedScheduleResponseMessage.ResponseMessage.Uri!, 
                httpHeaders, 
                null);

            httpRequestDefinitionList.Add(httpRequestDefinition);
        }

        var updatedHttpFileDocument = existingHttpFileDocument with
        {
            Requests = existingHttpFileDocument.Requests.Concat(httpRequestDefinitionList).ToList()
        };
        
        var updatedHttpFileDocumentString = httpFileRenderer.Render(
            updatedHttpFileDocument,
            new HttpFileRenderOptions(true, true));

        await File.WriteAllTextAsync(
            Path.Combine(BasePath, id),
            updatedHttpFileDocumentString,
            Encoding.UTF8, cancellationToken);
    }

    
    public async Task<HttpFileDocument> ReadAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        await using var fileStream = File.OpenRead(Path.Combine(BasePath, id));
        
        var httpFileDocument = await httpFileParser.ParseAsync(
            fileStream, new HttpFileParseOptions()
            {
              ResolveVariables  = true
            }, cancellationToken);

        return httpFileDocument;
    }

    public bool Exist(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentNullException(nameof(id));
        }
        return File.Exists(Path.Combine(BasePath, id));
    }

}


public sealed class RatedHttpFileRunner(
    IHttpClient httpClient, 
    HttpFileFactory httpFileFactory,
    IRatedScheduleRunner ratedScheduleRunner, 
    IRatedScheduleFactory ratedScheduleFactory, FileSystemHttpFileProvider fileSystemHttpFileProvider)
{
    public async Task<HttpFileDocument> Run(
        HttpFileDocument httpFileDocument, int rate, 
        TimeSpan rateWindow, 
        CancellationToken cancellationToken = default)
    {
        // Guard against empty requests collection.
        if(httpFileDocument.Requests.Count == 0)
        {
            return httpFileDocument;
        }

        var startTicks = Stopwatch.GetTimestamp();
        var startDate = DateTime.UtcNow;
        var callbacks = new ConcurrentDictionary<int, RatedScheduleResponseMessage?>();
        var httpRequestDefinitionCollection = httpFileDocument.Requests;

        // Remove all requests that have already been executed.
        var filteredHttpRequestDefinitionCollection = httpRequestDefinitionCollection
            .Where(httpRequestDefinition => httpRequestDefinition.Headers
                .FirstOrDefault(header =>
                    header.Name.StartsWith("X-Response", StringComparison.OrdinalIgnoreCase)) is null)
            .ToList();
        
        // Create a rated schedule based on the remaining requests.
        var ratedSchedule = ratedScheduleFactory.Create(new RatedScheduleOptions
        {
            Capacity = filteredHttpRequestDefinitionCollection.Count,
            Rate = rate,
            RateWindow = rateWindow
        });
        
        await using var completionLog = new BatchFlushQueue<RatedScheduleResponseMessage>(
            batchSize: 10,
            flushAsync: (items, ct) => File.AppendAllLinesAsync(
                "app.log",
                items.Select(CreateLogLine),
                ct),
            boundedCapacity: 10_000,
            cancellationToken: CancellationToken.None);

        await using var persistHttpContentQueue = new WorkQueue<CompletedHttpResponseMessage>(
            handlerAsync: async (completedHttpResponseMessage, ct) =>
            {
                await fileSystemHttpFileProvider.WriteAsync(completedHttpResponseMessage, ct);

                System.Console.WriteLine($"Processed {completedHttpResponseMessage.Uri}");
                
            },
            capacity: 1_000,
            maxDegreeOfParallelism: 8);        
        
        System.Console.WriteLine($"Execute {ratedSchedule.Capacity} requests, starting at: {startDate}.");
        System.Console.WriteLine($"Expected duration: {ratedSchedule.Duration}.");
        
        await ratedScheduleRunner.RunAsync(
            ratedSchedule.Offsets.ToList(),
            async (index, offset, innerCancellationToken) =>
            {
                innerCancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var request = httpFileDocument.Requests[index];
                    
                    // Get a completed response message(message containing contents as byte[] if body download was requested).
                    var completedHttpResponseMessage = await httpClient.SendAsync(new SerializableRequestMessage()
                    {
                        Method = HttpMethod.Head,
                        Uri = request.Url,
                        
                    }, innerCancellationToken);

                                        
                    
                    
                    // TODO: Persist
                    
                    var ratedScheduleResponseMessage = new RatedScheduleResponseMessage(
                        index, 
                        startDate.Add(offset), 
                        DateTime.UtcNow, 
                        completedHttpResponseMessage);                    
                    
                    callbacks.TryAdd(index, ratedScheduleResponseMessage);

                    var line = $"{index} {ratedScheduleResponseMessage.PlannedDateTime:hh:mm:ss.fff} {ratedScheduleResponseMessage.ExecutionDateTime:hh:mm:ss.fff} {ratedScheduleResponseMessage.ResponseMessage?.StatusCode} {ratedScheduleResponseMessage.ResponseMessage?.Method} { ratedScheduleResponseMessage.ResponseMessage?.Url}";

                    System.Console.WriteLine(line);
                    
                }
                catch (Exception)
                {
                    var ratedScheduleResponseMessage = new RatedScheduleResponseMessage(
                        index, 
                        startDate.Add(offset), 
                        DateTime.UtcNow, 
                        null);   
                    
                    callbacks.TryAdd(index, ratedScheduleResponseMessage);
                }
            },
            startTicks,
            maxDegreeOfParallelism: 100, 
            cancellationToken: cancellationToken);

        System.Console.WriteLine($"Successful callbacks {callbacks.Values.Count(x => x?.ResponseMessage?.StatusCode == HttpStatusCode.OK )}.");
        
        return httpFileFactory.Create(httpFileDocument, ratedSchedule, startDate, callbacks.ToDictionary());
        
    }
    
    public async Task<HttpFileDocument> Run(Uri uri, int rate, TimeSpan rateWindow, CancellationToken cancellationToken)
    {
        var httpFileDocumentFromUrl = await httpFileFactory.CreateAsync(uri, cancellationToken);
        httpFileDocumentFromUrl = httpFileDocumentFromUrl with
        {
            Requests = httpFileDocumentFromUrl.Requests.Skip(10).Take(10).ToList()
        };
        return await Run(httpFileDocumentFromUrl, rate, rateWindow, cancellationToken);
    }

    private static string CreateLogLine(RatedScheduleResponseMessage responseMessage)
    {
        return JsonSerializer.Serialize(responseMessage);
    }
}
