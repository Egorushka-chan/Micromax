using System.Collections.Concurrent;

namespace MicroMax.Server.Services.Assistant;

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
