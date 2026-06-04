using MicroMax.Server.Api.Stocks;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class StocksApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<StockBalanceResponse>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        if (warehouseIds.Count == 0)
        {
            return [];
        }

        return await db.StockBalances
            .Where(x => x.Quantity > 0 && warehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId))
            .OrderBy(x => x.Product!.Name)
            .ThenBy(x => x.StorageCell!.Code)
            .Select(x => new StockBalanceResponse(
                x.ProductId,
                x.Product!.Name,
                x.Product.Sku,
                x.Product.Unit,
                x.StorageCellId,
                x.StorageCell!.Code,
                x.StorageCell.StorageZone!.Code,
                x.Quantity))
            .ToListAsync(cancellationToken);
    }
}
