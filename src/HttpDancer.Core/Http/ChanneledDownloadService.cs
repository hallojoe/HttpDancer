using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace HttpDancer.Core.Http;

/// <summary>
/// Channel-based downloader with controlled concurrency and a total request budget.
/// - Concurrency: fixed worker pool (MaxConcurrentRequests).
/// - Budget: caps total scheduled downloads (MaxRequests).
/// - Dynamic enqueue: producers can add URLs anytime until the writer is completed.
/// - Dedupe: prevents duplicate scheduling and re-processing (case-insensitive).
/// - Pre-download hook: skip once / skip permanently / proceed.
/// - Completion callback per finished download.
/// </summary>
public class ChanneledDownloadService : IDownloadService
{
    private readonly ILogger<ChanneledDownloadService> _logger;
    private readonly IHttpClient _httpClient;
    private readonly DownloadContext _downloadContext;

    /// <summary>
    /// URLs that have been fully processed (success or permanently skipped). Case-insensitive.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _processedUrls = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// URLs that are scheduled/in-flight to prevent concurrent duplicates.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _scheduledUrls = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Unbounded channel -> no backpressure surprises; concurrency is set by worker count.
    /// </summary>
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    
    // Live counters/flags

    /// <summary>
    /// How many downloads have been reserved against MaxRequests?
    /// </summary>
    private int _requestCount;            
    
    /// <summary>
    /// Currently downloading
    /// </summary>
    private int _inFlight;                
    
    /// <summary>
    /// Items sitting in the channel
    /// </summary>
    private int _pending;                 

    /// <summary>
    /// 0/1: Ensure we only Complete the writer once.
    /// </summary>
    private int _writerCompleted;        
    
    private volatile bool _budgetExhausted;

    public ChanneledDownloadService(
        ILogger<ChanneledDownloadService> logger,
        IHttpClient httpClient,
        DownloadContext downloadContext)
    {
        _logger = logger;
        _httpClient = httpClient;
        _downloadContext = downloadContext;

        // Seed initial URLs
        EnqueueUrls(_downloadContext.Urls);
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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = cts.Token; // Capture the struct, not the CTS object else we get: Captured variable is disposed in the outer scope

        var workers = StartWorkers(_downloadContext.MaxConcurrentRequests, token);

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

            _logger.LogInformation("Download complete. Total processed: {Count}", _processedUrls.Count);

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
    /// Worker: pulls URLs from the channel, applies hooks/dedupe/budget, and downloads.
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
                    if (_downloadContext.RequestAsync is not null)
                    {
                        RequestDecision decision;
                        try
                        {
                            decision = await _downloadContext.RequestAsync(url).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Pre-download hook failed for {Url}. Defaulting to Proceed.", url);
                            
                            decision = RequestDecision.Proceed;
                        }

                        switch (decision)
                        {
                            case RequestDecision.SkipOnce:
                                _logger.LogInformation("Pre-download: skipping once for {Url}", url);
                                continue;

                            case RequestDecision.SkipPermanently:
                                _processedUrls.TryAdd(url, 0);
                                _logger.LogInformation("Pre-download: permanently skipped {Url}", url);
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
                    if (issued > _downloadContext.MaxRequests)
                    {
                        // Over the cap: roll back and mark budget exhausted.
                        Interlocked.Decrement(ref _requestCount);
                        
                        _scheduledUrls.TryRemove(url, out _);

                        _budgetExhausted = true;
                        
                        _logger.LogDebug("MaxRequests reached; not scheduling {Url}", url);
                        
                        continue;
                    }

                    Interlocked.Increment(ref _inFlight);
                    
                    _logger.LogInformation("Downloading: {Url} (#{Request})", url, issued);

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
            var resource = await _httpClient.GetAsync(url, token).ConfigureAwait(false);

            // Mark as processed on success.
            _processedUrls.TryAdd(url, 0);

            // Fire single response completion callback (best-effort).
            if (_downloadContext.ResponseAsync is not null)
            {
                var response = new DownloadResponse
                {
                    Url = url,
                    Value = resource
                };
                await _downloadContext.ResponseAsync(this, response).ConfigureAwait(false);
            }
            
            _logger.LogInformation("Downloaded: {Url}", url);
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

        if (_downloadContext.StatusAsync is not null)
        {
            var response = new StatusResponse
            {
                ProcessedCount = _processedUrls.Count,
                // Approximate remaining: bounded by leftover budget and pending queue items.
                RemainingCount = Math.Max(
                    0,
                    Math.Min(
                        _downloadContext.MaxRequests - Volatile.Read(ref _requestCount),
                        Volatile.Read(ref _pending)))
            };
            await _downloadContext.StatusAsync(this, response).ConfigureAwait(false);
        }
    }
}
