using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Работает со складскими ячейками и их содержимым.
/// </summary>
[Authorize]
[Route("api/cells")]
public sealed class CellsController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return Ok(await Db.StorageCells
            .Include(x => x.StorageZone)
            .ThenInclude(x => x!.Warehouse)
            .Where(x => warehouseIds.Contains(x.StorageZone!.WarehouseId))
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}/contents")]
    public async Task<IActionResult> GetContentsAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(id, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.StockRead,
            cancellationToken);

        return Ok(await Db.StockBalances
            .Include(x => x.Product)
            .Where(x => x.StorageCellId == id && x.Quantity > 0)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product!.Name,
                x.Product.Sku,
                x.Quantity,
                x.Product.Unit
            })
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<StorageCell>> CreateAsync([FromBody] StorageCell item, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseId = await warehousePermissionService.GetWarehouseIdForZoneAsync(item.StorageZoneId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await CreateEntityAsync(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(id, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await DeleteEntityAsync<StorageCell>(id);
    }
}
