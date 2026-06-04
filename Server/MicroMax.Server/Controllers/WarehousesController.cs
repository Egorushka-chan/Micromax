using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет справочником складов.
/// </summary>
[Authorize]
[Route("api/warehouses")]
public sealed class WarehousesController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Warehouse>>> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return Ok(await Db.Warehouses
            .Where(x => warehouseIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<Warehouse>> CreateAsync([FromBody] Warehouse item, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var adminRole = await warehousePermissionService.GetRequiredRoleAsync(SystemRoleCodes.Admin, cancellationToken);

        await using var tx = await Db.Database.BeginTransactionAsync(cancellationToken);
        Db.Warehouses.Add(item);
        await Db.SaveChangesAsync(cancellationToken);

        Db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = item.Id,
            UserId = userId,
            RoleId = adminRole.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] Warehouse input, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            id,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        var item = await Db.Warehouses.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = input.Name;
        item.Address = input.Address;
        await Db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            id,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await DeleteEntityAsync<Warehouse>(id);
    }
}
