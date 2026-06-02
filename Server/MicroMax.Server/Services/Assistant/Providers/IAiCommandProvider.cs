using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;

namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Общий контракт для реальных ИИ-провайдеров и Mock fallback.
/// </summary>
public interface IAiCommandProvider
{
    AiProviderKind Kind { get; }
    bool IsRealProvider { get; }
    Task<bool> ProbeAsync(CancellationToken cancellationToken);
    Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken);
}
