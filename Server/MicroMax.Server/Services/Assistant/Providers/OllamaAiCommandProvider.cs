using System.Diagnostics;
using MicroMax.Server.Services.Assistant.Configuration;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace MicroMax.Server.Services.Assistant.Providers;

public sealed class OllamaAiCommandProvider : ChatClientAiCommandProvider, IAiProviderFailurePolicy
{
    private readonly AiAssistantOptions _options;
    private readonly ILogger<OllamaAiCommandProvider> _logger;

    public OllamaAiCommandProvider(
        AiCommandPromptBuilder promptBuilder,
        IOptions<AiAssistantOptions> options,
        ILogger<OllamaAiCommandProvider> logger)
        : base(CreateClient(options.Value), promptBuilder, options, logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override AiProviderKind Kind => AiProviderKind.Ollama;

    public override async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(_options.HealthTimeoutMs));
            using var client = new HttpClient { BaseAddress = new Uri(_options.Ollama.BaseUrl) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/version");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Ollama probe failed. reason={Reason} elapsed_ms={ElapsedMs}",
                AiProviderFailureReasons.ProbeTimeout,
                started.ElapsedMilliseconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama probe failed. elapsed_ms={ElapsedMs}", started.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<bool> ShouldMarkUnavailableAsync(AiProviderException exception, CancellationToken cancellationToken)
    {
        if (!string.Equals(exception.ReasonCode, AiProviderFailureReasons.InferenceTimeout, StringComparison.Ordinal))
        {
            return true;
        }

        var isHealthy = await ProbeAsync(cancellationToken);
        if (isHealthy)
        {
            _logger.LogInformation(
                "Ollama stayed available after inference timeout. reason={Reason} elapsed_ms={ElapsedMs}",
                exception.ReasonCode,
                exception.Elapsed.TotalMilliseconds);
            return false;
        }

        return true;
    }

    private static IChatClient CreateClient(AiAssistantOptions options)
    {
        return new OllamaApiClient(new Uri(options.Ollama.BaseUrl), options.Ollama.Model);
    }
}
