namespace MicroMax.Server.Services.Assistant.Registry;

/// <summary>
/// Описывает команду помощника в одном месте: имя для ИИ, риск, подсказки и правила обязательных параметров.
/// </summary>
public sealed record AiCommandDefinition(
    string Type,
    string Title,
    string Description,
    string RiskLevel,
    bool RequiresProduct,
    bool RequiresSourceCell,
    bool RequiresTargetCell,
    bool RequiresQuantity,
    bool IsExecutable,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> TriggerPhrases);
