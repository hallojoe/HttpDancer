using System.Collections.Concurrent;
using System.Threading.Channels;
using HttpDancer.Core.Http.Clients;
using Microsoft.Extensions.Logging;

namespace HttpDancer.Core.Http.Downloading;

/// <summary>
/// Channel-backed downloader that separates concerns for clarity:
/// - Producers: any caller can enqueue URLs while the channel writer is open.
/// - Queue: unbounded channel holds URLs until workers can pick them up.
/// - Workers: fixed pool (MaxConcurrentRequests) that pulls URLs, skips duplicates, asks the hook if the URL should run, and then downloads.
/// - Budget: MaxRequests caps total downloads (not just concurrency); once reached, scheduling stops and the writer is completed.
/// - Hooks: optional RequestAsync to decide per-URL (skip once, skip permanently, or proceed).
/// - Callbacks: non-blocking ResponseAsync and StatusAsync run on a separate callback queue.
/// - Shutdown: monitor loop completes the writer when the budget is hit and the queue/in-flight work are empty.
/// </summary>
public class ChanneledDownloadService : IDownloadService
{
    private readonly ILogger<ChanneledDownloadService> _logger;
    private readonly IHttpClient _httpClient;
    private readonly ChanneledDownloadRequest _channeledDownloadRequest;

    /// <summary>
    /// URLs that have been fully processed (success or permanently skipped). Case-insensitive so "A" and "a" count as the same target.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _processedUrls = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// URLs currently scheduled or in-flight; prevents two workers from handling the same URL simultaneously.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _scheduledUrls = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Unbounded channel that buffers incoming URLs; worker count controls throughput instead of queue size.
    /// </summary>
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    /// <summary>
    /// Bounded callback queue so response/status handlers never block download workers; oldest callbacks are dropped if the queue overflows.
    /// </summary>
    private readonly Channel<CallbackWork> _callbackChannel = Channel.CreateBounded<CallbackWork>(new BoundedChannelOptions(4096)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    
    // Live counters/flags

    /// <summary>
    /// Total number of downloads that have been scheduled against MaxRequests (monotonic).
    /// </summary>
    private int _requestCount;            
    
    /// <summary>
    /// Count of worker operations currently downloading a URL.
    /// </summary>
    private int _inFlight;                
    
    /// <summary>
    /// Approximate items still sitting in the channel (enqueued minus dequeued).
    /// </summary>
    private int _pending;                 

    /// <summary>
    /// 0/1 guard so we only complete the channel writer a single time.
    /// </summary>
    private int _writerCompleted;        
    
    /// <summary>
    /// Set when the download budget is hit; tells the monitor to stop accepting new work.
    /// </summary>
    private volatile bool _budgetExhausted;

    public ChanneledDownloadService(
        ILogger<ChanneledDownloadService> logger,
        IHttpClient httpClient,
        ChanneledDownloadRequest channeledDownloadRequest)
    {
        _logger = logger;
        _httpClient = httpClient;
        _channeledDownloadRequest = channeledDownloadRequest;

        // Seed initial URLs
        EnqueueUrls(_channeledDownloadRequest.Urls);
    }

    /// <summary>
    /// Attempt to enqueue a single URL. Returns true if accepted.
    /// </summary>
    public bool EnqueueUrl(string? url)
    {
        // We allow duplicates here. Dedupe is enforced at scheduling time.

        if (string.IsNullOrWhiteSpace(url)) return false;
        
        var trimmed = url.Trim();
        
        // Writer may have been completed
        if (_channel.Writer.TryWrite(trimmed) is false) return false; 
        
        Interlocked.Increment(ref _pending);

        return true;
    }

    /// <summary>
    /// Enqueue multiple URLs (trim and distinct OrdinalIgnoreCase). Returns count accepted.
    /// </summary>
    public int EnqueueUrls(IEnumerable<string?>? urls)
    {
        if (urls is null) return 0;

        var added = 0;

        foreach (var url in urls
            .Where(s => string.IsNullOrWhiteSpace(s) is false)
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_channel.Writer.TryWrite(url)) continue;
            
            Interlocked.Increment(ref _pending);
            
            added++;
        }
        return added;
    }

    /// <summary>
    /// Run the download pipeline until:
    /// - Cancellation requested, OR
    /// - Request budget is exhausted, AND the channel is drained, AND no downloads are in-flight.
    /// </summary>
    public async Task<bool> DownloadAsync(CancellationToken cancellationToken = default)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Capture the struct, not the CTS object else we get: Captured variable is disposed in the outer scope
        var token = cancellationTokenSource.Token;

        var workers = StartWorkers(_channeledDownloadRequest.MaxConcurrentRequests, token);

        var callbackWorker = Task.Run(CallbackWorkerAsync);

        // Monitor loop: completes the writer when we're definitively done scheduling.
        var monitor = Task.Run(async () =>
        {

            try
            {

                while (!token.IsCancellationRequested)
                {
                    // If the budget is hit, we stop accepting new work.
                    if (_budgetExhausted)
                    {
                        TryCompleteWriter();
                    }

                    // Termination: budget exhausted AND nothing left to read AND nothing in flight.
                    if (_budgetExhausted &&
                        Volatile.Read(ref _pending) == 0 &&
                        Volatile.Read(ref _inFlight) == 0)
                    {
                        TryCompleteWriter();
                        break;
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            finally
            {
                // Ensure the writer is completed so workers can finish.
                TryCompleteWriter();
            }
        }, token);

        try
        {
            await Task.WhenAll(workers.Concat([monitor])).ConfigureAwait(false);

            _logger.LogDebug("Download complete. Total processed: {Count}", _processedUrls.Count);

            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download canceled. Completed: {Count}", _processedUrls.Count);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during downloads.");
            throw;
        }
        finally
        {
            _callbackChannel.Writer.TryComplete();
            try
            {
                await callbackWorker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during callback drain.
            }
        }
    }
    
    private void TryCompleteWriter()
    {
        if (Interlocked.Exchange(ref _writerCompleted, 1) == 0)
        {
            _channel.Writer.TryComplete();
        }
    }

    private IEnumerable<Task> StartWorkers(int maxConcurrency, CancellationToken token)
    {
        var workerCount = Math.Max(1, maxConcurrency);
        var workers = new List<Task>(capacity: workerCount);

        for (var i = 0; i < workerCount; i++)
        {
            workers.Add(Task.Run(() => WorkerLoopAsync(token), token));
        }

        return workers;
    }

    /// <summary>
    /// Worker flow: take a URL from the channel, skip if already processed, ask RequestAsync if present,
    /// block concurrent duplicates, reserve against the budget, download via IHttpClient, then queue callbacks.
    /// </summary>
    private async Task WorkerLoopAsync(CancellationToken token)
    {
        var reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var url))
                {
                    // We pulled one item from the channel.
                    Interlocked.Decrement(ref _pending);

                    token.ThrowIfCancellationRequested();

                    // Skip if already processed.
                    if (_processedUrls.ContainsKey(url))
                    {
                        _logger.LogDebug("Skipping already processed: {Url}", url);
                        
                        continue;
                    }

                    // Pre-download decision (skip once / permanently / proceed).
                    if (_channeledDownloadRequest.RequestAsync is not null)
                    {
                        RequestDecision decision;
                        try
                        {
                            decision = await _channeledDownloadRequest.RequestAsync(url).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Pre-download hook failed for {Url}. Defaulting to Proceed.", url);
                            
                            decision = RequestDecision.Proceed;
                        }

                        switch (decision)
                        {
                            case RequestDecision.SkipOnce:
                                _logger.LogDebug("Pre-download: skipping once for {Url}", url);
                                continue;

                            case RequestDecision.SkipPermanently:
                                _processedUrls.TryAdd(url, 0);
                                _logger.LogDebug("Pre-download: permanently skipped {Url}", url);
                                continue;

                            case RequestDecision.Proceed:
                                break;
                        }
                    }

                    // Ensure not scheduled concurrently.
                    if (!_scheduledUrls.TryAdd(url, 0))
                    {
                        _logger.LogDebug("Already scheduled/in-flight: {Url}", url);
                        
                        continue;
                    }

                    // Reserve against the overall request budget.
                    var issued = Interlocked.Increment(ref _requestCount);
                    if (issued > _channeledDownloadRequest.MaxRequests)
                    {
                        // Over the cap: roll back and mark budget exhausted.
                        Interlocked.Decrement(ref _requestCount);
                        
                        _scheduledUrls.TryRemove(url, out _);

                        _budgetExhausted = true;
                        
                        _logger.LogDebug("MaxRequests reached; not scheduling {Url}", url);
                        
                        continue;
                    }

                    Interlocked.Increment(ref _inFlight);
                    
                    _logger.LogDebug("Downloading: {Url} (#{Request})", url, issued);

                    try
                    {
                        await DownloadOneAsync(url, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _scheduledUrls.TryRemove(url, out _);
                        
                        Interlocked.Decrement(ref _inFlight);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal on shutdown.
        }
    }

    /// <summary>
    /// One download unit-of-work. Success marks processed and invokes completion-callback.
    /// </summary>
    private async Task DownloadOneAsync(string url, CancellationToken token)
    {
        try
        {
            Uri targetUri;
            try
            {
                targetUri = new Uri(url);
            }
            catch (UriFormatException uriEx)
            {
                _processedUrls.TryAdd(url, 0);
                _logger.LogWarning(uriEx, "Invalid URL format skipped: {Url}", url);
                return;
            }

            var requestMessage = new RequestMessage
            {
                Method = HttpMethod.Head,
                Uri = targetUri,
                ShouldReadBodyAsync = _channeledDownloadRequest.ShouldReadBodyAsync
            };

            var resource = await _httpClient.SendAsync(requestMessage, token).ConfigureAwait(false);
            
            // Mark as processed on success.
            _processedUrls.TryAdd(url, 0);

            // Fire single response completion callback (best-effort).
            if (_channeledDownloadRequest.ResponseAsync is not null)
            {
                    var response = new DownloadResponse
                {
                    Method = HttpMethod.Head.ToString(),
                    Url = url,
                    Value = resource
                };
                EnqueueCallback(new CallbackWork(CallbackKind.Response, response, null));
            }
            
            _logger.LogDebug("Downloaded: {Url}", url);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Download canceled for {Url}", url);
            // Not marked as processed: allows future retry in a new run.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error downloading {Url}", url);
            // Not marked processed: caller can re-enqueue or apply a retry policy externally.
        }

        if (_channeledDownloadRequest.StatusAsync is not null)
        {
            var processed = _processedUrls.Count;
            var pending = Volatile.Read(ref _pending);
            var inFlight = Volatile.Read(ref _inFlight);
            var scheduled = Volatile.Read(ref _requestCount);

            var response = new StatusResponse
            {
                ProcessedCount = processed,
                // Approximate remaining: bounded by leftover budget and pending queue items.
                RemainingCount = Math.Max(
                    0,
                    Math.Min(
                        _channeledDownloadRequest.MaxRequests - scheduled,
                        pending)),
                InFlightCount = inFlight,
                PendingCount = pending,
                ScheduledCount = scheduled,
                MaxRequests = _channeledDownloadRequest.MaxRequests
            };
            EnqueueCallback(new CallbackWork(CallbackKind.Status, null, response));
        }
    }

    private void EnqueueCallback(CallbackWork work)
    {
        if (_callbackChannel.Writer.TryWrite(work))
        {
            return;
        }

        _logger.LogWarning("Callback queue full; dropping {Kind}", work.Kind);
    }

    private async Task CallbackWorkerAsync()
    {
        var reader = _callbackChannel.Reader;
        
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var work))
                {
                    try
                    {
                        switch (work.Kind)
                        {
                            case CallbackKind.Response when _channeledDownloadRequest.ResponseAsync is not null:
                                await _channeledDownloadRequest.ResponseAsync(this, work.Response!).ConfigureAwait(false);
                                break;

                            case CallbackKind.Status when _channeledDownloadRequest.StatusAsync is not null:
                                await _channeledDownloadRequest.StatusAsync(this, work.Status!).ConfigureAwait(false);
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Error running {Kind} callback.", work.Kind);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private enum CallbackKind
    {
        Response,
        Status
    }

    private readonly record struct CallbackWork(CallbackKind Kind, DownloadResponse? Response, StatusResponse? Status);
}
