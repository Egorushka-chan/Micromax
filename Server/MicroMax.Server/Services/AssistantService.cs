using System.Collections.Concurrent;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services;

public sealed class AssistantService(MicroMaxDbContext db, AiProviderSelector providerSelector)
{
    private static readonly ConcurrentDictionary<string, AssistantCommand> PendingCommands = new();

    public async Task<AssistantCommand> InterpretAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Команда не должна быть пустой.");
        }

        var context = new AiCommandContext(
            await db.Products.OrderBy(x => x.Name).ToListAsync(cancellationToken),
            await db.StorageCells.OrderBy(x => x.Code).ToListAsync(cancellationToken));

        var command = await providerSelector.InterpretAsync(context, text, cancellationToken);
        command.CommandId = Guid.NewGuid().ToString("N");

        if (command.RequiresConfirmation)
        {
            PendingCommands[command.CommandId] = command;
        }

        return command;
    }

    public static bool TryTakePendingCommand(string commandId, out AssistantCommand? command) =>
        PendingCommands.Remove(commandId, out command);
}
