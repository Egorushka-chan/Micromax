using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет зоны хранения.
/// </summary>
[Authorize]
[Route("api/zones")]
public sealed class ZonesController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StorageZone>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return Ok(await Db.StorageZones
            .Include(x => x.Warehouse)
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<StorageZone>> CreateAsync([FromBody] StorageZone item, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            item.WarehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await CreateEntityAsync(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseId = await warehousePermissionService.GetWarehouseIdForZoneAsync(id, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await DeleteEntityAsync<StorageZone>(id);
    }
}
