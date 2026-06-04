using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает актуальные остатки по ячейкам хранения.
/// </summary>
[Authorize]
[Route("api/stocks")]
public sealed class StocksController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        if (warehouseIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        return Ok(await Db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.Quantity > 0 && warehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId))
            .OrderBy(x => x.Product!.Name)
            .ThenBy(x => x.StorageCell!.Code)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product!.Name,
                x.Product.Sku,
                x.Product.Unit,
                CellId = x.StorageCellId,
                CellCode = x.StorageCell!.Code,
                ZoneCode = x.StorageCell.StorageZone!.Code,
                x.Quantity
            })
            .ToListAsync(cancellationToken));
    }
}
