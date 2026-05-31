using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services;

public sealed class WarehouseOperationService(MicroMaxDbContext db)
{
    public Task<WarehouseOperation> ReceiveAsync(ReceiveRequest request) =>
        ExecuteAsync(WarehouseOperationType.Receive, request.ProductId, null, request.TargetCellId, request.Quantity, request.UserId, request.Comment);

    public Task<WarehouseOperation> MoveAsync(MoveRequest request) =>
        ExecuteAsync(WarehouseOperationType.Move, request.ProductId, request.SourceCellId, request.TargetCellId, request.Quantity, request.UserId, request.Comment);

    public Task<WarehouseOperation> WriteOffAsync(WriteOffRequest request) =>
        ExecuteAsync(WarehouseOperationType.WriteOff, request.ProductId, request.SourceCellId, null, request.Quantity, request.UserId, request.Comment);

    private async Task<WarehouseOperation> ExecuteAsync(
        WarehouseOperationType type,
        int productId,
        int? sourceCellId,
        int? targetCellId,
        decimal quantity,
        int? userId,
        string? comment)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Количество должно быть положительным.");
        }

        if (!await db.Products.AnyAsync(x => x.Id == productId))
        {
            throw new InvalidOperationException("Номенклатура не найдена.");
        }

        if (sourceCellId is not null && !await db.StorageCells.AnyAsync(x => x.Id == sourceCellId))
        {
            throw new InvalidOperationException("Исходная ячейка не найдена.");
        }

        if (targetCellId is not null && !await db.StorageCells.AnyAsync(x => x.Id == targetCellId))
        {
            throw new InvalidOperationException("Целевая ячейка не найдена.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        if (sourceCellId is not null)
        {
            var sourceBalance = await GetBalanceAsync(productId, sourceCellId.Value);
            if (sourceBalance.Quantity < quantity)
            {
                throw new InvalidOperationException("Недостаточный остаток в исходной ячейке.");
            }

            sourceBalance.Quantity -= quantity;
        }

        if (targetCellId is not null)
        {
            var targetBalance = await GetOrCreateBalanceAsync(productId, targetCellId.Value);
            targetBalance.Quantity += quantity;
        }

        var operation = new WarehouseOperation
        {
            Type = type,
            ProductId = productId,
            SourceCellId = sourceCellId,
            TargetCellId = targetCellId,
            AppUserId = userId,
            Quantity = quantity,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.WarehouseOperations.Add(operation);
        db.OperationLogs.Add(new OperationLog
        {
            WarehouseOperation = operation,
            Message = BuildLogMessage(type, quantity),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return operation;
    }

    private async Task<StockBalance> GetBalanceAsync(int productId, int cellId)
    {
        return await db.StockBalances
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.StorageCellId == cellId)
            ?? throw new InvalidOperationException("Остаток в исходной ячейке не найден.");
    }

    private async Task<StockBalance> GetOrCreateBalanceAsync(int productId, int cellId)
    {
        var balance = await db.StockBalances
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.StorageCellId == cellId);

        if (balance is not null)
        {
            return balance;
        }

        balance = new StockBalance
        {
            ProductId = productId,
            StorageCellId = cellId,
            Quantity = 0
        };
        db.StockBalances.Add(balance);
        return balance;
    }

    private static string BuildLogMessage(WarehouseOperationType type, decimal quantity) => type switch
    {
        WarehouseOperationType.Receive => $"Приёмка: увеличен остаток на {quantity}.",
        WarehouseOperationType.Move => $"Перемещение: перенесено {quantity}.",
        WarehouseOperationType.WriteOff => $"Списание: уменьшен остаток на {quantity}.",
        _ => "Складская операция выполнена."
    };
}
