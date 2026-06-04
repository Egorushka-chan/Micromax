using MicroMax.Server.Api.Cells;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class CellsApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<StorageCellResponse>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return await db.StorageCells
            .Where(x => warehouseIds.Contains(x.StorageZone!.WarehouseId))
            .OrderBy(x => x.Code)
            .Select(x => new StorageCellResponse(
                x.Id,
                x.StorageZoneId,
                x.StorageZone!.WarehouseId,
                x.Code,
                x.Name,
                x.StorageZone.Code,
                x.StorageZone.Warehouse!.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CellContentsItemResponse>> GetContentsAsync(
        int userId,
        int cellId,
        CancellationToken cancellationToken = default)
    {
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(cellId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.StockRead,
            cancellationToken);

        return await db.StockBalances
            .Where(x => x.StorageCellId == cellId && x.Quantity > 0)
            .Select(x => new CellContentsItemResponse(
                x.ProductId,
                x.Product!.Name,
                x.Product.Sku,
                x.Quantity,
                x.Product.Unit))
            .ToListAsync(cancellationToken);
    }

    public async Task<StorageCellResponse> CreateAsync(
        int userId,
        CreateStorageCellRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseId = await warehousePermissionService.GetWarehouseIdForZoneAsync(request.StorageZoneId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        var code = request.Code.Trim();
        if (await db.StorageCells.AnyAsync(
                x => x.StorageZoneId == request.StorageZoneId && x.Code == code,
                cancellationToken))
        {
            throw new ApiConflictException("Ячейка с таким кодом уже существует в выбранной зоне.");
        }

        var cell = new StorageCell
        {
            StorageZoneId = request.StorageZoneId,
            Code = code,
            Name = request.Name.Trim()
        };

        db.StorageCells.Add(cell);
        await db.SaveChangesAsync(cancellationToken);

        return await GetRequiredResponseAsync(cell.Id, cancellationToken);
    }

    public async Task DeleteAsync(int userId, int cellId, CancellationToken cancellationToken = default)
    {
        var cell = await db.StorageCells
            .Include(x => x.StorageZone)
            .FirstOrDefaultAsync(x => x.Id == cellId, cancellationToken)
            ?? throw new ApiNotFoundException("Ячейка хранения не найдена.");

        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            cell.StorageZone!.WarehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        var activeBarcodes = await db.Barcodes
            .Where(x => x.IsActive && x.EntityType == BarcodeEntityType.Cell && x.EntityId == cellId)
            .ToListAsync(cancellationToken);

        foreach (var barcode in activeBarcodes)
        {
            barcode.IsActive = false;
            barcode.IsPrimary = false;
        }

        db.StorageCells.Remove(cell);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<StorageCellResponse> GetRequiredResponseAsync(int cellId, CancellationToken cancellationToken)
    {
        return await db.StorageCells
            .Where(x => x.Id == cellId)
            .Select(x => new StorageCellResponse(
                x.Id,
                x.StorageZoneId,
                x.StorageZone!.WarehouseId,
                x.Code,
                x.Name,
                x.StorageZone.Code,
                x.StorageZone.Warehouse!.Name))
            .FirstAsync(cancellationToken);
    }
}
