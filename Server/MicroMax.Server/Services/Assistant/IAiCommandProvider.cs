using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant;

public interface IAiCommandProvider
{
    AiProviderKind Kind { get; }
    bool IsRealProvider { get; }
    Task<bool> ProbeAsync(CancellationToken cancellationToken);
    Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken);
}
