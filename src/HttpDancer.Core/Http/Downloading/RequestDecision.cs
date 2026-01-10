namespace HttpDancer.Core.Http.Downloading;

/// <summary>
/// Decision returned by the pre-download hook for a given URL.
/// </summary>
public enum RequestDecision
{
    /// <summary>
    /// Continue with download.
    /// </summary>
    Proceed,          
    /// <summary>
    /// Skip this time, but allow future attempts.
    /// </summary>
    SkipOnce,         
    /// <summary>
    /// Skip and mark as processed. No retries.
    /// </summary>
    SkipPermanently   
}