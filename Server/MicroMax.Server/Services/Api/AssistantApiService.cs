using MicroMax.Server.Api.Assistant;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Api;

public sealed class AssistantApiService(
    AiCommandRegistry registry,
    AssistantService assistantService,
    AssistantCommandExecutionService assistantCommandExecutionService)
{
    public IReadOnlyList<AssistantCommandDefinitionResponse> GetCommands() =>
        registry.Commands
            .Select(x => new AssistantCommandDefinitionResponse(
                x.Type,
                x.Title,
                x.Description,
                x.RiskLevel,
                x.Examples))
            .ToList();

    public Task<AssistantCommandResponse> InterpretAsync(
        int userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default) =>
        InterpretAsync(userId, null, request, cancellationToken);

    public async Task<AssistantCommandResponse> InterpretAsync(
        int userId,
        int? warehouseId,
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = await assistantService.InterpretAsync(userId, warehouseId, request.Text, cancellationToken);
        return ToResponse(command);
    }

    public Task<AssistantCommandResultResponse> ConfirmAsync(
        int userId,
        AssistantConfirmationRequest request,
        CancellationToken cancellationToken = default) =>
        ConfirmAsync(userId, null, request, cancellationToken);

    public async Task<AssistantCommandResultResponse> ConfirmAsync(
        int userId,
        int? warehouseId,
        AssistantConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
        {
            var cancelled = warehouseId is null
                ? AssistantService.TryCancelPendingCommand(request.CommandId, userId)
                : AssistantService.TryCancelPendingCommand(request.CommandId, userId, warehouseId.Value);

            if (!cancelled)
            {
                throw new ApiNotFoundException("Команда не найдена или уже обработана.");
            }

            return new AssistantCommandResultResponse(true, "Команда отменена.", []);
        }

        AssistantCommand? command;
        var hasPendingCommand = warehouseId is null
            ? AssistantService.TryTakePendingCommand(request.CommandId, userId, out command)
            : AssistantService.TryTakePendingCommand(request.CommandId, userId, warehouseId.Value, out command);

        if (!hasPendingCommand || command is null)
        {
            throw new ApiNotFoundException("Команда не найдена или уже обработана.");
        }

        var operation = await assistantCommandExecutionService.ExecuteAsync(command, userId, warehouseId, cancellationToken);

        return new AssistantCommandResultResponse(
            true,
            "Команда подтверждена и выполнена.",
            [operation is null ? command.Summary : $"Операция #{operation.Id}: {operation.Type}"]);
    }

    public Task<AssistantCommandResponse> ClarifyAsync(
        int userId,
        AssistantClarificationRequest request,
        CancellationToken cancellationToken = default) =>
        ClarifyAsync(userId, null, request, cancellationToken);

    public async Task<AssistantCommandResponse> ClarifyAsync(
        int userId,
        int? warehouseId,
        AssistantClarificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = await assistantService.ClarifyAsync(userId, warehouseId, request.CommandId, request.ChoiceId, cancellationToken);
        return ToResponse(command);
    }

    private static AssistantCommandResponse ToResponse(AssistantCommand command) =>
        new(
            command.CommandId,
            command.Mode,
            command.Provider,
            command.CommandType,
            command.RiskLevel,
            command.ProductId,
            command.SourceCellId,
            command.TargetCellId,
            command.Quantity,
            command.MinQuantity,
            command.Summary,
            command.RequiresConfirmation,
            command.ClarificationQuestion,
            command.ClarificationTarget,
            command.Choices
                .Select(choice => new AssistantChoiceResponse(choice.Id, choice.Label, choice.Kind))
                .ToList());
}
