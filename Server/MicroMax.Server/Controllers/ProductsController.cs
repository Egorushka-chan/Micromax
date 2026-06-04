using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет номенклатурой и местонахождением товаров.
/// </summary>
[Authorize]
[Route("api/products")]
public sealed class ProductsController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureAnyWarehouseAccessAsync(userId, cancellationToken);
        return Ok(await Db.Products.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}/locations")]
    public async Task<IActionResult> GetLocationsAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return Ok(await Db.StockBalances
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.ProductId == id && x.Quantity > 0 && warehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId))
            .Select(x => new
            {
                CellId = x.StorageCellId,
                CellCode = x.StorageCell!.Code,
                ZoneCode = x.StorageCell.StorageZone!.Code,
                x.Quantity
            })
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateAsync([FromBody] Product item, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        return await CreateEntityAsync(item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] Product input, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);

        var item = await Db.Products.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        item.Sku = input.Sku;
        item.Name = input.Name;
        item.Unit = input.Unit;
        item.MinQuantity = input.MinQuantity;
        await Db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        return await DeleteEntityAsync<Product>(id);
    }
}
