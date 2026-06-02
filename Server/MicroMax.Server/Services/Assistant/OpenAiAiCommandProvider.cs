using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroMax.Server.Models;
using OpenAiChatClient = OpenAI.Chat.ChatClient;

namespace MicroMax.Server.Services.Assistant;

public sealed class OpenAiAiCommandProvider(
    IConfiguration configuration,
    AiCommandPromptBuilder promptBuilder,
    IOptions<AiAssistantOptions> options,
    ILogger<OpenAiAiCommandProvider> logger) : IAiCommandProvider
{
    private readonly AiAssistantOptions _options = options.Value;

    public AiProviderKind Kind => AiProviderKind.OpenAi;
    public bool IsRealProvider => true;

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(_options.HealthTimeoutMs));
            var response = await client.GetResponseAsync("Верни только JSON: {\"ok\":true}", cancellationToken: timeout.Token);
            return response.Text.Contains("ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "OpenAI probe failed.");
            return false;
        }
    }

    public async Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));

        var response = await client.GetResponseAsync(promptBuilder.Build(context, text), cancellationToken: timeout.Token);
        var json = ExtractJson(response.Text);
        return JsonSerializer.Deserialize<AssistantCommand>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("ИИ вернул пустую команду.");
    }

    private IChatClient CreateClient()
    {
        var apiKey = _options.OpenAi.ApiKey
            ?? configuration["OPENAI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        return new OpenAiChatClient(_options.OpenAi.Model, apiKey).AsIChatClient();
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
