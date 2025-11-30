namespace HttpDancer.Core.Http.Observability;

public interface ICorrelationIdProvider
{
    /// <summary>
    /// Returns the correlation id for the current execution flow.
    /// Implementation is responsible for ensuring this is stable
    /// throughout one logical operation.
    /// </summary>
    string GetCorrelationId();
}