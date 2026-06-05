using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Configuration;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Базовый провайдер для моделей, подключенных через Microsoft.Extensions.AI IChatClient.
/// </summary>
public abstract class ChatClientAiCommandProvider(
    IChatClient chatClient,
    AiCommandPromptBuilder promptBuilder,
    IOptions<AiAssistantOptions> options,
    ILogger logger) : IAiCommandProvider
{
    private readonly AiAssistantOptions _options = options.Value;

    public abstract AiProviderKind Kind { get; }
    public bool IsRealProvider => true;

    public virtual async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(_options.HealthTimeoutMs));
            var response = await chatClient.GetResponseAsync("Верни только JSON: {\"ok\":true}", cancellationToken: timeout.Token);
            return response.Text.Contains("ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "AI provider {Provider} probe failed. reason={Reason} elapsed_ms={ElapsedMs}",
                Kind,
                AiProviderFailureReasons.ProbeTimeout,
                started.ElapsedMilliseconds);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "AI provider {Provider} probe failed. elapsed_ms={ElapsedMs}", Kind, started.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));

        try
        {
            var prompt = promptBuilder.Build(context, text);
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: timeout.Token);
            return DeserializeCommand(response.Text, started.Elapsed);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException(
                AiProviderFailureReasons.InferenceTimeout,
                started.Elapsed,
                $"AI provider {Kind} inference timed out.",
                ex);
        }
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var fenced = Regex.Match(trimmed, "```(?:json)?\\s*(\\{[\\s\\S]*?\\})\\s*```", RegexOptions.IgnoreCase);
        if (fenced.Success)
        {
            return fenced.Groups[1].Value;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        throw new InvalidOperationException("ИИ не вернул JSON-команду.");
    }

    private static AssistantCommand DeserializeCommand(string text, TimeSpan elapsed)
    {
        try
        {
            var json = ExtractJson(text);
            return JsonSerializer.Deserialize<AssistantCommand>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new JsonException("AI returned an empty command.");
        }
        catch (JsonException ex)
        {
            throw new AiProviderException(
                AiProviderFailureReasons.InvalidJson,
                elapsed,
                "AI returned invalid JSON.",
                ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new AiProviderException(
                AiProviderFailureReasons.InvalidJson,
                elapsed,
                "AI returned invalid JSON.",
                ex);
        }
    }
}
