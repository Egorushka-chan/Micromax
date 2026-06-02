namespace MicroMax.Server.Services.Assistant;

public sealed class AiAssistantOptions
{
    public OllamaOptions Ollama { get; set; } = new();
    public OpenAiOptions OpenAi { get; set; } = new();
    public int ProviderProbeIntervalSeconds { get; set; } = 30;
    public int HealthTimeoutMs { get; set; } = 1500;
    public int InferenceTimeoutSeconds { get; set; } = 25;
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3-vl:8b";
}

public sealed class OpenAiOptions
{
    public string Model { get; set; } = "gpt-4o-mini";
    public string? ApiKey { get; set; }
}
