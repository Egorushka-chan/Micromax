using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Assistant.Registry;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Обрабатывает команды складского помощника и подтверждение операций.
/// </summary>
[Route("api/assistant")]
public sealed class AssistantController(
    AiCommandRegistry registry,
    AssistantService assistantService,
    WarehouseOperationService operations,
    MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet("commands")]
    public IActionResult GetCommands() =>
        Ok(registry.Commands);

    [HttpPost("interpret")]
    public async Task<IActionResult> InterpretAsync([FromBody] AssistantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await assistantService.InterpretAsync(request.Text, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            return InvalidOperation(ex);
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAsync([FromBody] AssistantConfirmationRequest request)
    {
        if (!request.Confirmed)
        {
            return Ok(new AssistantCommandResult(true, "Команда отменена.", []));
        }

        if (!AssistantService.TryTakePendingCommand(request.CommandId, out var command) || command is null)
        {
            return NotFound(new { error = "Команда не найдена или уже обработана." });
        }

        try
        {
            var operation = await ExecuteAssistantCommandAsync(command);

            return Ok(new AssistantCommandResult(
                true,
                "Команда подтверждена и выполнена.",
                [operation is null ? command.Summary : $"Операция #{operation.Id}: {operation.Type}"]));
        }
        catch (InvalidOperationException ex)
        {
            return InvalidOperation(ex);
        }
    }

    private Task<WarehouseOperation?> ExecuteAssistantCommandAsync(AssistantCommand command) =>
        command.CommandType switch
        {
            "post_receipt" => ReceiveFromCommandAsync(command),
            "move_product" => MoveFromCommandAsync(command),
            "write_off_product" => WriteOffFromCommandAsync(command),
            "create_product" => CreateProductFromCommandAsync(command),
            "update_min_stock" => UpdateMinQuantityFromCommandAsync(command),
            _ => throw new InvalidOperationException("Команда не содержит достаточных данных для выполнения.")
        };

    private async Task<WarehouseOperation?> CreateProductFromCommandAsync(AssistantCommand command)
    {
        Db.Products.Add(new Product
        {
            Sku = command.Sku!.Trim(),
            Name = command.Name!.Trim(),
            Unit = string.IsNullOrWhiteSpace(command.Unit) ? "шт" : command.Unit.Trim(),
            MinQuantity = command.MinQuantity ?? 0
        });

        await Db.SaveChangesAsync();
        return null;
    }

    private async Task<WarehouseOperation?> UpdateMinQuantityFromCommandAsync(AssistantCommand command)
    {
        var product = await Db.Products.FindAsync(command.ProductId!.Value)
            ?? throw new InvalidOperationException("Номенклатура не найдена.");

        product.MinQuantity = command.MinQuantity!.Value;
        await Db.SaveChangesAsync();
        return null;
    }

    private async Task<WarehouseOperation?> ReceiveFromCommandAsync(AssistantCommand command)
    {
        Ensure(command.ProductId, command.TargetCellId, command.Quantity);
        return await operations.ReceiveAsync(new ReceiveRequest(
            command.ProductId!.Value,
            command.TargetCellId!.Value,
            command.Quantity!.Value,
            null,
            command.Summary));
    }

    private async Task<WarehouseOperation?> MoveFromCommandAsync(AssistantCommand command)
    {
        Ensure(command.ProductId, command.SourceCellId, command.TargetCellId, command.Quantity);
        return await operations.MoveAsync(new MoveRequest(
            command.ProductId!.Value,
            command.SourceCellId!.Value,
            command.TargetCellId!.Value,
            command.Quantity!.Value,
            null,
            command.Summary));
    }

    private async Task<WarehouseOperation?> WriteOffFromCommandAsync(AssistantCommand command)
    {
        Ensure(command.ProductId, command.SourceCellId, command.Quantity);
        return await operations.WriteOffAsync(new WriteOffRequest(
            command.ProductId!.Value,
            command.SourceCellId!.Value,
            command.Quantity!.Value,
            null,
            command.Summary));
    }

    private static void Ensure(params object?[] values)
    {
        if (values.Any(x => x is null))
        {
            throw new InvalidOperationException("Команда не содержит достаточных данных для выполнения.");
        }
    }
}
