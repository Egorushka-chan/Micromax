namespace MicroMax.Server.Services.Assistant.Core;

public static class AiProviderPriorities
{
    public static int GetSortOrder(AiProviderKind kind) => kind switch
    {
        AiProviderKind.OpenAi => 1,
        AiProviderKind.Ollama => 2,
        AiProviderKind.Mock => 3,
        _ => int.MaxValue
    };
}
