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
    WarehousePermissionService warehousePermissionService)
{
    private static readonly ConcurrentDictionary<string, PendingAssistantCommand> PendingCommands = new();

    public async Task<AssistantCommand> InterpretAsync(int userId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApiValidationException("Команда не должна быть пустой.");
        }

        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        var context = new AiCommandContext(
            await db.Products.OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await db.StorageCells
                .Where(x => warehouseIds.Contains(x.StorageZone!.WarehouseId))
                .OrderBy(x => x.Code)
                .ToListAsync(cancellationToken));

        var command = await providerSelector.InterpretAsync(context, text, cancellationToken);
        command.CommandId = Guid.NewGuid().ToString("N");

        if (command.RequiresConfirmation)
        {
            PendingCommands[command.CommandId] = new PendingAssistantCommand(userId, command);
        }

        return command;
    }

    public static bool TryTakePendingCommand(string commandId, int userId, out AssistantCommand? command)
    {
        command = null;
        if (!PendingCommands.TryGetValue(commandId, out var pendingCommand) || pendingCommand.UserId != userId)
        {
            return false;
        }

        if (!PendingCommands.TryRemove(commandId, out pendingCommand))
        {
            return false;
        }

        command = pendingCommand.Command;
        return true;
    }

    private sealed record PendingAssistantCommand(int UserId, AssistantCommand Command);
}
