using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using HttpDancer.Core.Http.Clients;
using HttpDancer.FileFormats.HttpFile;
using HttpDancer.Scheduling.RatedScheduling;

namespace HttpDancer.Console.Workflows;

public record RatedScheduleResponseMessage(
    int Index, 
    DateTime PlannedDateTime, 
    DateTime? ExecutionDateTime, 
    ResponseMessage? ResponseMessage);

public sealed class RatedHttpFileRunner(
    IHttpClient httpClient, 
    HttpFileFactory httpFileFactory,
    IRatedScheduleRunner ratedScheduleRunner, 
    IRatedScheduleFactory ratedScheduleFactory)
{
    public async Task<HttpFileDocument> Run(HttpFileDocument httpFileDocument, int rate, TimeSpan rateWindow, CancellationToken cancellationToken = default)
    {
        if(httpFileDocument.Requests.Count == 0)
        {
            return httpFileDocument;
        }

        var startTicks = Stopwatch.GetTimestamp();
        var startDate = DateTime.UtcNow;
        var callbacks = new ConcurrentDictionary<int, RatedScheduleResponseMessage?>();
        var requests = httpFileDocument.Requests;

        // Remove all requests that have already been executed.
        var filteredRequests = requests
            .Where(httpRequestDefinition => httpRequestDefinition.Headers
                .FirstOrDefault(header =>
                    header.Name.StartsWith("X-Response", StringComparison.OrdinalIgnoreCase)) is null)
            .ToList();
        
        var ratedSchedule = ratedScheduleFactory.Create(new RatedScheduleOptions
        {
            Capacity = filteredRequests.Count,
            Rate = rate,
            RateWindow = rateWindow
        });
        
        System.Console.WriteLine($"Execute {ratedSchedule.Capacity} requests, starting at: {startDate}.");
        System.Console.WriteLine($"Expected duration: {ratedSchedule.Duration}.");
        
        await ratedScheduleRunner.RunAsync(
            ratedSchedule.Offsets
                .ToList(),
            async (index, offset, innerCancellationToken) =>
            {
                try
                {
                    innerCancellationToken.ThrowIfCancellationRequested();

                    var request = httpFileDocument.Requests[index];
                    
                    var responseMessage = await httpClient.HeadAsync(request.Url, innerCancellationToken);

                    responseMessage.BodyBytes = null;

                    var ratedScheduleResponseMessage = new RatedScheduleResponseMessage(
                        index, 
                        startDate.Add(offset), 
                        DateTime.UtcNow, 
                        responseMessage);                    
                    
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
}