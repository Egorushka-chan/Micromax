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

    public async Task<AssistantCommandResponse> InterpretAsync(
        int userId,
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = await assistantService.InterpretAsync(userId, request.Text, cancellationToken);
        return ToResponse(command);
    }

    public async Task<AssistantCommandResultResponse> ConfirmAsync(
        int userId,
        AssistantConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
        {
            return new AssistantCommandResultResponse(true, "Команда отменена.", []);
        }

        if (!AssistantService.TryTakePendingCommand(request.CommandId, userId, out var command) || command is null)
        {
            throw new ApiNotFoundException("Команда не найдена или уже обработана.");
        }

        var operation = await assistantCommandExecutionService.ExecuteAsync(command, userId, cancellationToken);

        return new AssistantCommandResultResponse(
            true,
            "Команда подтверждена и выполнена.",
            [operation is null ? command.Summary : $"Операция #{operation.Id}: {operation.Type}"]);
    }

    private static AssistantCommandResponse ToResponse(AssistantCommand command) =>
        new(
            command.CommandId,
            command.Mode,
            command.Provider,
            command.CommandType,
            command.RiskLevel,
            command.Summary,
            command.RequiresConfirmation,
            command.ClarificationQuestion,
            command.Choices
                .Select(choice => new AssistantChoiceResponse(choice.Id, choice.Label, choice.Kind))
                .ToList());
}
