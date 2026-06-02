using System.Text.Json;
using System.Text.RegularExpressions;
using MicroMax.Server.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MicroMax.Server.Services.Assistant;

public abstract class ChatClientAiCommandProvider(
    IChatClient chatClient,
    AiCommandPromptBuilder promptBuilder,
    IOptions<AiAssistantOptions> options,
    ILogger logger) : IAiCommandProvider
{
    private readonly AiAssistantOptions _options = options.Value;

    public abstract AiProviderKind Kind { get; }
    public bool IsRealProvider => true;

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(_options.HealthTimeoutMs));
            var response = await chatClient.GetResponseAsync("Верни только JSON: {\"ok\":true}", cancellationToken: timeout.Token);
            return response.Text.Contains("ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "AI provider {Provider} probe failed.", Kind);
            return false;
        }
    }

    public async Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));

        var prompt = promptBuilder.Build(context, text);
        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: timeout.Token);
        var json = ExtractJson(response.Text);

        return JsonSerializer.Deserialize<AssistantCommand>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("ИИ вернул пустую команду.");
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
}
