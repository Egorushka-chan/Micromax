using MicroMax.Server.Api.Operations;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class AssistantCommandExecutionService(
    Data.MicroMaxDbContext db,
    WarehouseOperationService warehouseOperationService,
    WarehousePermissionService warehousePermissionService)
{
    public Task<WarehouseOperation?> ExecuteAsync(
        AssistantCommand command,
        int userId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(command, userId, null, cancellationToken);

    public Task<WarehouseOperation?> ExecuteAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken = default) =>
        command.CommandType switch
        {
            "post_receipt" => ReceiveFromCommandAsync(command, userId, warehouseId, cancellationToken),
            "move_product" => MoveFromCommandAsync(command, userId, warehouseId, cancellationToken),
            "write_off_product" => WriteOffFromCommandAsync(command, userId, warehouseId, cancellationToken),
            "create_product" => CreateProductFromCommandAsync(command, userId, warehouseId, cancellationToken),
            "update_min_stock" => UpdateMinQuantityFromCommandAsync(command, userId, warehouseId, cancellationToken),
            _ => throw new ApiValidationException("Команда не содержит достаточных данных для выполнения.")
        };

    private async Task<WarehouseOperation?> CreateProductFromCommandAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, warehouseId.Value, cancellationToken);
        }

        Ensure(command.Sku, command.Name);

        var sku = command.Sku!.Trim();
        if (await db.Products.AnyAsync(x => x.Sku == sku, cancellationToken))
        {
            throw new ApiConflictException("Номенклатура с таким SKU уже существует.");
        }

        db.Products.Add(new Product
        {
            Sku = sku,
            Name = command.Name!.Trim(),
            Unit = string.IsNullOrWhiteSpace(command.Unit) ? "шт" : command.Unit.Trim(),
            MinQuantity = command.MinQuantity ?? 0
        });

        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<WarehouseOperation?> UpdateMinQuantityFromCommandAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, warehouseId.Value, cancellationToken);
        }

        Ensure(command.ProductId, command.MinQuantity);

        var product = await db.Products.FindAsync([command.ProductId!.Value], cancellationToken)
            ?? throw new ApiNotFoundException("Номенклатура не найдена.");

        product.MinQuantity = command.MinQuantity!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<WarehouseOperation?> ReceiveFromCommandAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        Ensure(command.ProductId, command.TargetCellId, command.Quantity);

        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureOperationAccessAsync(userId, null, command.TargetCellId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                userId,
                warehouseId.Value,
                WarehousePermission.OperationsExecute,
                cancellationToken);
            await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
                warehouseId.Value,
                null,
                command.TargetCellId,
                cancellationToken);
        }

        return await warehouseOperationService.ReceiveAsync(
            new ReceiveRequest(
                command.ProductId!.Value,
                command.TargetCellId!.Value,
                command.Quantity!.Value,
                command.Summary),
            userId,
            cancellationToken);
    }

    private async Task<WarehouseOperation?> MoveFromCommandAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        Ensure(command.ProductId, command.SourceCellId, command.TargetCellId, command.Quantity);

        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureOperationAccessAsync(
                userId,
                command.SourceCellId,
                command.TargetCellId,
                cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                userId,
                warehouseId.Value,
                WarehousePermission.OperationsExecute,
                cancellationToken);
            await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
                warehouseId.Value,
                command.SourceCellId,
                command.TargetCellId,
                cancellationToken);
        }

        return await warehouseOperationService.MoveAsync(
            new MoveRequest(
                command.ProductId!.Value,
                command.SourceCellId!.Value,
                command.TargetCellId!.Value,
                command.Quantity!.Value,
                command.Summary),
            userId,
            cancellationToken);
    }

    private async Task<WarehouseOperation?> WriteOffFromCommandAsync(
        AssistantCommand command,
        int userId,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        Ensure(command.ProductId, command.SourceCellId, command.Quantity);

        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureOperationAccessAsync(userId, command.SourceCellId, null, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                userId,
                warehouseId.Value,
                WarehousePermission.OperationsExecute,
                cancellationToken);
            await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
                warehouseId.Value,
                command.SourceCellId,
                null,
                cancellationToken);
        }

        return await warehouseOperationService.WriteOffAsync(
            new WriteOffRequest(
                command.ProductId!.Value,
                command.SourceCellId!.Value,
                command.Quantity!.Value,
                command.Summary),
            userId,
            cancellationToken);
    }

    private static void Ensure(params object?[] values)
    {
        if (values.Any(x => x is null))
        {
            throw new ApiValidationException("Команда не содержит достаточных данных для выполнения.");
        }
    }
}
