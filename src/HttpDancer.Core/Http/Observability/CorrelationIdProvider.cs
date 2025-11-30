using System.Diagnostics;

namespace HttpDancer.Core.Http.Observability;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public string GetCorrelationId()
    {
        if (!string.IsNullOrWhiteSpace(CurrentCorrelationId.Value))
        {
            return CurrentCorrelationId.Value!;
        }

        // Prefer Activity.TraceId if present. Ensure compatibility with common tracing and telemetry things.
        var activityTraceId = Activity.Current?.TraceId.ToString();
        var correlationId = !string.IsNullOrWhiteSpace(activityTraceId)
            ? activityTraceId!
            : Guid.NewGuid().ToString("N");

        CurrentCorrelationId.Value = correlationId;

        return correlationId;
    }
}