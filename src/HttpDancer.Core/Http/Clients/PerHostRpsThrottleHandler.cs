using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HttpDancer.Core.Http.Clients;

/// <summary>
/// Simple per-host RPS throttling via PartitionedRateLimiter.
/// Each host gets its own token bucket (burst and refill).
/// </summary>
/// <remarks>
/// All classes needed when operating with this handler is in this file.
/// </remarks>
public sealed class PerHostRpsThrottleHandler : DelegatingHandler
{
    private readonly PartitionedRateLimiter<HttpRequestMessage> _limiter;
    private readonly ILogger<PerHostRpsThrottleHandler> _logger;

    public PerHostRpsThrottleHandler(
        IOptionsMonitor<PerHostRpsThrottleOptions> options,
        ILogger<PerHostRpsThrottleHandler> logger)
    {
        _logger = logger;

        // Create a partitioned limiter: one bucket per host (and optionally per port).
        _limiter = PartitionedRateLimiter.Create<HttpRequestMessage, string>(request =>
        {
            var opt = options.CurrentValue;

            var uri = request.RequestUri;
            if (uri is null)
            {
                // No URI: don't throttle (or you could put under an "unknown" key)
                return RateLimitPartition.GetNoLimiter("no-uri");
            }

            // Example partition key: host (or host:port if you want stricter separation)
            var key = opt.IncludePortInKey ? $"{uri.Host}:{uri.Port}" : uri.Host;

            // If you want different limits per host, you can consult opt.Overrides here:
            var (rps, burst) = opt.TryGetLimit(key);

            // Token bucket where:
            // - Tokens are replenished at RPS (per second)
            // - Each request consumes 1 token
            // - BurstCapacity controls short spikes
            return RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: key,
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = burst,                    // max tokens in bucket (burst)
                    TokensPerPeriod = rps,                 // refill amount each period
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    AutoReplenishment = true,
                    QueueLimit = opt.QueueLimit,           // how many can wait
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var lease = await _limiter.AcquireAsync(request, permitCount: 1, cancellationToken).ConfigureAwait(false);

        if (!lease.IsAcquired)
        {
            // If the limiter is configured with QueueLimit=0, you might hit this when at capacity.
            // With QueueLimit > 0, you'll normally wait and acquire.
            _logger.LogWarning("Per-host throttle rejected request to {Uri}", request.RequestUri);
            throw new HttpRequestException("Request throttled (rate limit lease not acquired).");
        }

        // Optional: log delay/metrics if lease contains metadata
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Options for per-host throttling.
/// </summary>
public sealed class PerHostRpsThrottleOptions
{
    public const string Key = "PerHostRpsHttpHandler";

    /// <summary>Default requests per second per host.</summary>
    public int DefaultRps { get; set; } = 2;

    /// <summary>Default burst capacity per host.</summary>
    public int DefaultBurst { get; set; }

    /// <summary>
    /// Queue length per host while waiting for tokens.
    /// Set to 0 to fail fast (throws), or a positive number to wait.
    /// </summary>
    public int QueueLimit { get; set; } = 1024;

    /// <summary>If true, then the partition key is host:port instead of host.</summary>
    public bool IncludePortInKey { get; set; } = false;

    /// <summary>
    /// Optional per-host overrides. Key must match the partition key (host or host:port).
    /// </summary>
    public IDictionary<string, HostLimit> Overrides { get; set; } = new Dictionary<string, HostLimit>(StringComparer.OrdinalIgnoreCase);

    public (int rps, int burst) TryGetLimit(string key)
    {
        if (Overrides.TryGetValue(key, out var limit))
        {
            var rps = Math.Max(1, limit.Rps);
            var burst = Math.Max(1, limit.Burst > 0 ? limit.Burst : rps);
            return (rps, burst);
        }

        return (Math.Max(1, DefaultRps), Math.Max(1, DefaultBurst > 0 ? DefaultBurst : DefaultRps));
    }
}

public sealed class HostLimit
{
    public int Rps { get; init; }
    public int Burst { get; init; }
}
