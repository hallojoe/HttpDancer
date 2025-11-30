namespace HttpDancer.Core.Configuration;

// ReSharper disable once ClassNeverInstantiated.Global
public class ApiClientSettings 
{
    public const string Key = "ApiClient";

    public bool? ReadBodyOnSuccess { get; set; } = null;

    public bool? ReadBodyOnNonSuccess { get; set; } = null;

    /// <summary>
    /// Whether to include a correlation id header on outgoing HTTP requests.
    /// </summary>
    public bool IncludeCorrelationIdHeader { get; set; } = true;
    
    /// <summary>
    /// The HTTP header name used for correlation id propagation.
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";
    
    public string? BaseUrl { get; set; }

    public string? Job { get; set; }
    
    public string[] AllowedHosts { get; set; } = [];
    
    public required IoSettings Io { get; set; }
    
    public MinificationSettings? Minification { get; set; }
}

public class IoSettings
{
    public const string Key = "Io";
    public bool Enabled { get; set; }
    public string? Workspace { get; set; }
    public required string[] HrefListPaths { get; set; }
    public required string? KnownHrefListPath { get; set; }
    
    public required string? SeenUrls { get; set; }
}


public class MinificationSettings
{
    public bool Enabled { get; set; }
    public List<string>? RemoveSelectors { get; set; }
}
