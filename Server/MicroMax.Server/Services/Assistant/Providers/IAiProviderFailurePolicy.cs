namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Optional provider policy for deciding whether a failure should mark the provider unavailable.
/// </summary>
public interface IAiProviderFailurePolicy
{
    Task<bool> ShouldMarkUnavailableAsync(AiProviderException exception, CancellationToken cancellationToken);
}
