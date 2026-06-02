namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Provider failure with a reason code and elapsed time for fallback logging.
/// </summary>
public sealed class AiProviderException(
    string reasonCode,
    TimeSpan elapsed,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ReasonCode { get; } = reasonCode;
    public TimeSpan Elapsed { get; } = elapsed;
}
