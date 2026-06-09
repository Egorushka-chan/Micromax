using System.Collections.Concurrent;
using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Execution;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services;

public sealed class AssistantService(
    MicroMaxDbContext db,
    AiProviderSelector providerSelector,
    WarehousePermissionService warehousePermissionService,
    AiCommandNormalizer normalizer)
{
    private static readonly ConcurrentDictionary<string, PendingAssistantCommand> PendingCommands = new();
    private static readonly ConcurrentDictionary<string, PendingAssistantCommand> PendingClarifications = new();

    public Task<AssistantCommand> InterpretAsync(int userId, string text, CancellationToken cancellationToken = default) =>
        InterpretAsync(userId, null, text, cancellationToken);

    public async Task<AssistantCommand> InterpretAsync(
        int userId,
        int? warehouseId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApiValidationException("Команда не должна быть пустой.");
        }

        var context = await BuildContextAsync(userId, warehouseId, cancellationToken);
        var command = await providerSelector.InterpretAsync(context, text, cancellationToken);
        command.CommandId = Guid.NewGuid().ToString("N");
        StorePendingCommand(userId, warehouseId, command);

        return command;
    }

    public Task<AssistantCommand> ClarifyAsync(
        int userId,
        string commandId,
        string choiceId,
        CancellationToken cancellationToken = default) =>
        ClarifyAsync(userId, null, commandId, choiceId, cancellationToken);

    public async Task<AssistantCommand> ClarifyAsync(
        int userId,
        int? warehouseId,
        string commandId,
        string choiceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(choiceId))
        {
            throw new ApiValidationException("Вариант для уточнения не выбран.");
        }

        if (!TryTakePendingClarification(commandId, userId, warehouseId, out var command) || command is null)
        {
            throw new ApiNotFoundException("Команда не найдена или уже обработана.");
        }

        var context = await BuildContextAsync(userId, warehouseId, cancellationToken);
        var updatedCommand = ApplyChoice(command, choiceId);
        var providerKind = ParseProviderKind(updatedCommand.Provider);
        var normalizedCommand = normalizer.Normalize(updatedCommand, context, providerKind);
        normalizedCommand.CommandId = Guid.NewGuid().ToString("N");
        StorePendingCommand(userId, warehouseId, normalizedCommand);

        return normalizedCommand;
    }

    public static bool TryTakePendingCommand(string commandId, int userId, out AssistantCommand? command) =>
        TryTakePending(PendingCommands, commandId, userId, null, out command);

    public static bool TryTakePendingCommand(string commandId, int userId, int warehouseId, out AssistantCommand? command) =>
        TryTakePending(PendingCommands, commandId, userId, warehouseId, out command);

    public static bool TryCancelPendingCommand(string commandId, int userId) =>
        TryRemovePending(PendingCommands, commandId, userId, null) ||
        TryRemovePending(PendingClarifications, commandId, userId, null);

    public static bool TryCancelPendingCommand(string commandId, int userId, int warehouseId) =>
        TryRemovePending(PendingCommands, commandId, userId, warehouseId) ||
        TryRemovePending(PendingClarifications, commandId, userId, warehouseId);

    private static bool TryTakePendingClarification(string commandId, int userId, int? warehouseId, out AssistantCommand? command)
    {
        return TryTakePending(PendingClarifications, commandId, userId, warehouseId, out command);
    }

    private async Task<AiCommandContext> BuildContextAsync(
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> warehouseIds;
        if (warehouseId is null)
        {
            warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureWarehouseAccessAsync(userId, warehouseId.Value, cancellationToken);
            warehouseIds = [warehouseId.Value];
        }

        return new AiCommandContext(
            await db.Products.OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await db.StorageCells
                .Where(x => warehouseIds.Contains(x.StorageZone!.WarehouseId))
                .OrderBy(x => x.Code)
                .ToListAsync(cancellationToken));
    }

    private static void StorePendingCommand(int userId, int? warehouseId, AssistantCommand command)
    {
        if (command.RequiresConfirmation)
        {
            PendingCommands[command.CommandId] = new PendingAssistantCommand(userId, warehouseId, command);
            return;
        }

        if (command.Mode == "Clarification" && command.Choices.Count > 0)
        {
            PendingClarifications[command.CommandId] = new PendingAssistantCommand(userId, warehouseId, command);
        }
    }

    private static AssistantCommand ApplyChoice(AssistantCommand command, string choiceId)
    {
        var choice = command.Choices.FirstOrDefault(x => x.Id.Equals(choiceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiValidationException("Выбранный вариант недоступен.");

        var updated = CloneCommand(command);
        var target = updated.ClarificationTarget?.Trim();

        switch (target)
        {
            case "Product":
                updated.ProductId = ParseChoiceId(choice);
                break;

            case "SourceCell":
                updated.SourceCellId = ParseChoiceId(choice);
                break;

            case "TargetCell":
                updated.TargetCellId = ParseChoiceId(choice);
                break;

            case "Command":
                updated.CommandType = choice.Id;
                break;

            default:
                ApplyChoiceByKind(updated, choice);
                break;
        }

        updated.Mode = "Command";
        updated.ClarificationQuestion = null;
        updated.ClarificationTarget = null;
        updated.Choices = [];

        return updated;
    }

    private static void ApplyChoiceByKind(AssistantCommand command, AssistantChoice choice)
    {
        switch (choice.Kind.ToLowerInvariant())
        {
            case "product":
                command.ProductId = ParseChoiceId(choice);
                break;

            case "cell":
            {
                var cellId = ParseChoiceId(choice);
                if (command.SourceCellId is null && command.TargetCellId is not null)
                {
                    command.SourceCellId = cellId;
                    break;
                }

                if (command.TargetCellId is null)
                {
                    command.TargetCellId = cellId;
                    break;
                }

                command.SourceCellId = cellId;
                break;
            }

            case "command":
                command.CommandType = choice.Id;
                break;

            default:
                throw new ApiValidationException("Выбранный вариант нельзя применить к команде.");
        }
    }

    private static int ParseChoiceId(AssistantChoice choice)
    {
        if (int.TryParse(choice.Id, out var value))
        {
            return value;
        }

        throw new ApiValidationException("Выбранный вариант некорректен.");
    }

    private static AssistantCommand CloneCommand(AssistantCommand command)
    {
        return new AssistantCommand
        {
            CommandId = command.CommandId,
            Mode = command.Mode,
            Provider = command.Provider,
            CommandType = command.CommandType,
            RiskLevel = command.RiskLevel,
            ProductId = command.ProductId,
            SourceCellId = command.SourceCellId,
            TargetCellId = command.TargetCellId,
            Quantity = command.Quantity,
            MinQuantity = command.MinQuantity,
            Sku = command.Sku,
            Name = command.Name,
            Unit = command.Unit,
            RequiresConfirmation = command.RequiresConfirmation,
            Summary = command.Summary,
            ClarificationQuestion = command.ClarificationQuestion,
            ClarificationTarget = command.ClarificationTarget,
            Choices = command.Choices.Select(choice => new AssistantChoice(choice.Id, choice.Label, choice.Kind)).ToList()
        };
    }

    private static AiProviderKind ParseProviderKind(string? provider)
    {
        return Enum.TryParse<AiProviderKind>(provider, true, out var providerKind)
            ? providerKind
            : AiProviderKind.Mock;
    }

    private static bool TryTakePending(
        ConcurrentDictionary<string, PendingAssistantCommand> store,
        string commandId,
        int userId,
        int? warehouseId,
        out AssistantCommand? command)
    {
        command = null;
        if (!store.TryGetValue(commandId, out var pendingCommand) ||
            pendingCommand.UserId != userId ||
            pendingCommand.WarehouseId != warehouseId)
        {
            return false;
        }

        if (!store.TryRemove(commandId, out pendingCommand))
        {
            return false;
        }

        command = pendingCommand.Command;
        return true;
    }

    private static bool TryRemovePending(
        ConcurrentDictionary<string, PendingAssistantCommand> store,
        string commandId,
        int userId,
        int? warehouseId)
    {
        if (!store.TryGetValue(commandId, out var pendingCommand) ||
            pendingCommand.UserId != userId ||
            pendingCommand.WarehouseId != warehouseId)
        {
            return false;
        }

        return store.TryRemove(commandId, out _);
    }

    private sealed record PendingAssistantCommand(int UserId, int? WarehouseId, AssistantCommand Command);
}
