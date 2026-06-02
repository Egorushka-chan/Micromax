using MicroMax.Server.Services.Assistant.Configuration;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Providers;
using Microsoft.Extensions.Options;

namespace MicroMax.Server.Services.Assistant.Recovery;

/// <summary>
/// Фоновая проверка восстановления Ollama/OpenAI, чтобы приложение не оставалось в Mock-режиме навсегда.
/// </summary>
public sealed class AiProviderRecoveryService(
    IServiceProvider serviceProvider,
    AiProviderAvailability availability,
    IOptions<AiAssistantOptions> options,
    ILogger<AiProviderRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.ProviderProbeIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var providers = scope.ServiceProvider
                    .GetServices<IAiCommandProvider>()
                    .Where(x => x.IsRealProvider)
                    .OrderBy(x => AiProviderPriorities.GetSortOrder(x.Kind))
                    .ToList();

                foreach (var provider in providers)
                {
                    if (availability.IsAvailable(provider.Kind))
                    {
                        continue;
                    }

                    if (await provider.ProbeAsync(stoppingToken))
                    {
                        availability.MarkAvailable(provider.Kind);
                        logger.LogInformation("AI provider {Provider} recovered.", provider.Kind);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "AI provider recovery probe failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
