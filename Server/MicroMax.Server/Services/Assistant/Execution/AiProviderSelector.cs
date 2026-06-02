using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Providers;
using MicroMax.Server.Services.Assistant.Recovery;

namespace MicroMax.Server.Services.Assistant.Execution;

/// <summary>
/// Выбирает провайдера по цепочке Ollama -> OpenAI -> Mock и фиксирует падение реальных провайдеров.
/// </summary>
public sealed class AiProviderSelector(
    IEnumerable<IAiCommandProvider> providers,
    AiProviderAvailability availability,
    AiCommandNormalizer normalizer,
    ILogger<AiProviderSelector> logger)
{
    private readonly IReadOnlyList<IAiCommandProvider> _providers = [.. providers.OrderBy(static x => AiProviderPriorities.GetSortOrder(x.Kind))];

    public async Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            if (provider.IsRealProvider && !availability.IsAvailable(provider.Kind))
            {
                continue;
            }

            try
            {
                var command = await provider.InterpretAsync(context, text, cancellationToken);
                if (provider.IsRealProvider)
                {
                    availability.MarkAvailable(provider.Kind);
                }

                return normalizer.Normalize(command, context, provider.Kind);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (provider.IsRealProvider)
            {
                var providerException = ex as AiProviderException;
                var shouldMarkUnavailable = true;

                if (providerException is not null && provider is IAiProviderFailurePolicy failurePolicy)
                {
                    shouldMarkUnavailable = await failurePolicy.ShouldMarkUnavailableAsync(providerException, cancellationToken);
                }

                if (shouldMarkUnavailable)
                {
                    availability.MarkUnavailable(provider.Kind);
                }

                logger.LogWarning(
                    ex,
                    "AI provider {Provider} failed. Falling back. reason={Reason} elapsed_ms={ElapsedMs} mark_unavailable={MarkUnavailable}",
                    provider.Kind,
                    providerException?.ReasonCode ?? "provider_error",
                    providerException?.Elapsed.TotalMilliseconds,
                    shouldMarkUnavailable);
            }
        }

        var mock = _providers.First(x => x.Kind == AiProviderKind.Mock);
        var fallbackCommand = await mock.InterpretAsync(context, text, cancellationToken);
        return normalizer.Normalize(fallbackCommand, context, AiProviderKind.Mock);
    }

    public IReadOnlyList<IAiCommandProvider> RealProviders => _providers.Where(x => x.IsRealProvider).ToList();
}
