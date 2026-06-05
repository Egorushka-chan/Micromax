namespace MicroMax.Server.Api.Assistant;

public sealed record AssistantRequest(string Text);

public sealed record AssistantConfirmationRequest(string CommandId, bool Confirmed);

public sealed record AssistantClarificationRequest(string CommandId, string ChoiceId);

public sealed record AssistantCommandResponse(
    string CommandId,
    string Mode,
    string Provider,
    string CommandType,
    string RiskLevel,
    int? ProductId,
    int? SourceCellId,
    int? TargetCellId,
    decimal? Quantity,
    decimal? MinQuantity,
    string Summary,
    bool RequiresConfirmation,
    string? ClarificationQuestion,
    string? ClarificationTarget,
    IReadOnlyList<AssistantChoiceResponse> Choices);

public sealed record AssistantChoiceResponse(
    string Id,
    string Label,
    string Kind);

public sealed record AssistantCommandDefinitionResponse(
    string Type,
    string Title,
    string Description,
    string RiskLevel,
    IReadOnlyList<string> Examples);

public sealed record AssistantCommandResultResponse(
    bool Success,
    string Message,
    IReadOnlyList<string> Details);
