using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant.Core;

/// <summary>
/// Минимальный складской контекст, который передаётся ИИ для распознавания команды.
/// </summary>
public sealed record AiCommandContext(
    IReadOnlyList<Product> Products,
    IReadOnlyList<StorageCell> Cells);

public enum AiProviderKind
{
    Ollama = 1,
    OpenAi = 2,
    Mock = 3
}
