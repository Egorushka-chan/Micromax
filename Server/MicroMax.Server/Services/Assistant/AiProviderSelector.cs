using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant;

public sealed class AiProviderSelector(
    IEnumerable<IAiCommandProvider> providers,
    AiProviderAvailability availability,
    AiCommandNormalizer normalizer,
    ILogger<AiProviderSelector> logger)
{
    private readonly IReadOnlyList<IAiCommandProvider> _providers = providers
        .OrderBy(x => x.Kind)
        .ToList();

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
            catch (Exception ex) when (provider.IsRealProvider)
            {
                availability.MarkUnavailable(provider.Kind);
                logger.LogWarning(ex, "AI provider {Provider} failed. Falling back.", provider.Kind);
            }
        }

        var mock = _providers.First(x => x.Kind == AiProviderKind.Mock);
        var fallbackCommand = await mock.InterpretAsync(context, text, cancellationToken);
        return normalizer.Normalize(fallbackCommand, context, AiProviderKind.Mock);
    }

    public IReadOnlyList<IAiCommandProvider> RealProviders => _providers.Where(x => x.IsRealProvider).ToList();
}
