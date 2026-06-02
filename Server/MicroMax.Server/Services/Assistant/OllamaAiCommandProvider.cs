using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace MicroMax.Server.Services.Assistant;

public sealed class OllamaAiCommandProvider : ChatClientAiCommandProvider
{
    public OllamaAiCommandProvider(
        AiCommandPromptBuilder promptBuilder,
        IOptions<AiAssistantOptions> options,
        ILogger<OllamaAiCommandProvider> logger)
        : base(CreateClient(options.Value), promptBuilder, options, logger)
    {
    }

    public override AiProviderKind Kind => AiProviderKind.Ollama;

    private static IChatClient CreateClient(AiAssistantOptions options)
    {
        return new OllamaApiClient(new Uri(options.Ollama.BaseUrl), options.Ollama.Model);
    }
}
