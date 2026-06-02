using System.Collections.Concurrent;
using MicroMax.Server.Services.Assistant.Core;

namespace MicroMax.Server.Services.Assistant.Recovery;

/// <summary>
/// Хранит текущее состояние доступности провайдеров между пользовательскими запросами и recovery-проверками.
/// </summary>
public sealed class AiProviderAvailability
{
    private readonly ConcurrentDictionary<AiProviderKind, bool> _state = new();

    public bool IsAvailable(AiProviderKind kind)
    {
        return _state.GetOrAdd(kind, true);
    }

    public void MarkAvailable(AiProviderKind kind)
    {
        _state[kind] = true;
    }

    public void MarkUnavailable(AiProviderKind kind)
    {
        _state[kind] = false;
    }
}
