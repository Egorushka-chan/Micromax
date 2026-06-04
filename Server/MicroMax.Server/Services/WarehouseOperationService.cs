using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services;

public sealed class WarehouseOperationService(MicroMaxDbContext db)
{
    public Task<WarehouseOperation> ReceiveAsync(ReceiveRequest request, int userId) =>
        ExecuteAsync(
            WarehouseOperationType.Receive,
            request.ProductId,
            null,
            request.TargetCellId,
            request.Quantity,
            userId,
            request.Comment);

    public Task<WarehouseOperation> MoveAsync(MoveRequest request, int userId) =>
        ExecuteAsync(
            WarehouseOperationType.Move,
            request.ProductId,
            request.SourceCellId,
            request.TargetCellId,
            request.Quantity,
            userId,
            request.Comment);

    public Task<WarehouseOperation> WriteOffAsync(WriteOffRequest request, int userId) =>
        ExecuteAsync(
            WarehouseOperationType.WriteOff,
            request.ProductId,
            request.SourceCellId,
            null,
            request.Quantity,
            userId,
            request.Comment);

    public async Task<WarehouseOperation> AdjustAsync(AdjustRequest request, int userId)
    {
        if (request.TargetQuantity < 0)
        {
            throw new InvalidOperationException("Итоговый остаток не может быть отрицательным.");
        }

        await EnsureProductExistsAsync(request.ProductId);
        await EnsureCellExistsAsync(request.TargetCellId, "Целевая ячейка не найдена.");

        await using var tx = await db.Database.BeginTransactionAsync();

        var targetBalance = await GetOrCreateBalanceAsync(request.ProductId, request.TargetCellId);
        var currentQuantity = targetBalance.Quantity;
        var adjustmentQuantity = Math.Abs(request.TargetQuantity - currentQuantity);

        if (adjustmentQuantity == 0)
        {
            throw new InvalidOperationException("Текущий остаток уже соответствует указанному значению.");
        }

        targetBalance.Quantity = request.TargetQuantity;

        var operation = new WarehouseOperation
        {
            Type = WarehouseOperationType.Adjust,
            WarehouseId = await GetWarehouseIdFromCellsAsync(null, request.TargetCellId),
            ProductId = request.ProductId,
            TargetCellId = request.TargetCellId,
            AppUserId = userId,
            Quantity = adjustmentQuantity,
            Comment = BuildAdjustComment(request.Comment, currentQuantity, request.TargetQuantity),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.WarehouseOperations.Add(operation);
        db.OperationLogs.Add(new OperationLog
        {
            WarehouseOperation = operation,
            Message = BuildAdjustLogMessage(currentQuantity, request.TargetQuantity),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return operation;
    }

    private async Task<WarehouseOperation> ExecuteAsync(
        WarehouseOperationType type,
        int productId,
        int? sourceCellId,
        int? targetCellId,
        decimal quantity,
        int userId,
        string? comment)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Количество должно быть положительным.");
        }

        await EnsureProductExistsAsync(productId);

        if (sourceCellId is not null)
        {
            await EnsureCellExistsAsync(sourceCellId.Value, "Исходная ячейка не найдена.");
        }

        if (targetCellId is not null)
        {
            await EnsureCellExistsAsync(targetCellId.Value, "Целевая ячейка не найдена.");
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
            WarehouseId = await GetWarehouseIdFromCellsAsync(sourceCellId, targetCellId),
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

    private async Task EnsureProductExistsAsync(int productId)
    {
        if (!await db.Products.AnyAsync(x => x.Id == productId))
        {
            throw new InvalidOperationException("Номенклатура не найдена.");
        }
    }

    private async Task EnsureCellExistsAsync(int cellId, string errorMessage)
    {
        if (!await db.StorageCells.AnyAsync(x => x.Id == cellId))
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private async Task<int> GetWarehouseIdFromCellsAsync(int? sourceCellId, int? targetCellId)
    {
        var targetWarehouseId = targetCellId is null
            ? (int?)null
            : await db.StorageCells
                .Where(x => x.Id == targetCellId.Value)
                .Select(x => x.StorageZone!.WarehouseId)
                .FirstAsync();
        var sourceWarehouseId = sourceCellId is null
            ? (int?)null
            : await db.StorageCells
                .Where(x => x.Id == sourceCellId.Value)
                .Select(x => x.StorageZone!.WarehouseId)
                .FirstAsync();

        var warehouseId = targetWarehouseId ?? sourceWarehouseId
            ?? throw new InvalidOperationException("Не удалось определить склад для операции.");

        if (targetWarehouseId is not null && sourceWarehouseId is not null && targetWarehouseId != sourceWarehouseId)
        {
            throw new InvalidOperationException("Складская операция должна выполняться в рамках одного склада.");
        }

        return warehouseId;
    }

    private static string BuildLogMessage(WarehouseOperationType type, decimal quantity) => type switch
    {
        WarehouseOperationType.Receive => $"Приёмка: увеличен остаток на {quantity}.",
        WarehouseOperationType.Move => $"Перемещение: перенесено {quantity}.",
        WarehouseOperationType.WriteOff => $"Списание: уменьшен остаток на {quantity}.",
        WarehouseOperationType.Adjust => $"Корректировка: остаток изменён на {quantity}.",
        _ => "Складская операция выполнена."
    };

    private static string BuildAdjustComment(string? comment, decimal currentQuantity, decimal targetQuantity)
    {
        var adjustNote = $"Корректировка до {targetQuantity} (было {currentQuantity}).";
        return string.IsNullOrWhiteSpace(comment) || comment.Trim() == "Операция из мобильного приложения"
            ? adjustNote
            : $"{comment.Trim()} {adjustNote}";
    }

    private static string BuildAdjustLogMessage(decimal currentQuantity, decimal targetQuantity) =>
        $"Корректировка: остаток изменён с {currentQuantity} до {targetQuantity}.";
}
