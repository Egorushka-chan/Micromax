using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouses/{warehouseId:int}/users")]
public sealed class WarehouseUsersController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WarehouseUserResponse>>> GetAsync(
        int warehouseId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.UsersManage,
            cancellationToken);

        var users = await db.WarehouseUsers
            .Where(x => x.WarehouseId == warehouseId)
            .OrderBy(x => x.User!.DisplayName)
            .Select(x => new WarehouseUserResponse(
                x.UserId,
                x.User!.Email,
                x.User.DisplayName,
                x.User.IsActive,
                x.Role!.Code,
                x.Role.Name,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<WarehouseUserResponse>> CreateAsync(
        int warehouseId,
        [FromBody] AddWarehouseUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = currentUserService.GetRequiredUserId(User);
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                currentUserId,
                warehouseId,
                WarehousePermission.UsersManage,
                cancellationToken);

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var targetUser = await db.AppUsers.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken)
                ?? throw new InvalidOperationException("Пользователь с указанным email не найден.");
            var role = await warehousePermissionService.GetRequiredRoleAsync(request.RoleCode, cancellationToken);

            if (await db.WarehouseUsers.AnyAsync(
                    x => x.WarehouseId == warehouseId && x.UserId == targetUser.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException("Пользователь уже добавлен в этот склад.");
            }

            var membership = new WarehouseUser
            {
                WarehouseId = warehouseId,
                UserId = targetUser.Id,
                RoleId = role.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.WarehouseUsers.Add(membership);
            await db.SaveChangesAsync(cancellationToken);

            return Ok(new WarehouseUserResponse(
                targetUser.Id,
                targetUser.Email,
                targetUser.DisplayName,
                targetUser.IsActive,
                role.Code,
                role.Name,
                membership.CreatedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{userId:int}/role")]
    public async Task<IActionResult> UpdateRoleAsync(
        int warehouseId,
        int userId,
        [FromBody] UpdateWarehouseUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = currentUserService.GetRequiredUserId(User);
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                currentUserId,
                warehouseId,
                WarehousePermission.UsersManage,
                cancellationToken);

            var membership = await db.WarehouseUsers
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException("Пользователь не найден в выбранном складе.");

            var role = await warehousePermissionService.GetRequiredRoleAsync(request.RoleCode, cancellationToken);
            membership.RoleId = role.Id;
            await db.SaveChangesAsync(cancellationToken);

            return Ok(new WarehouseUserResponse(
                membership.UserId,
                (await db.AppUsers.Where(x => x.Id == membership.UserId).Select(x => x.Email).FirstAsync(cancellationToken)),
                (await db.AppUsers.Where(x => x.Id == membership.UserId).Select(x => x.DisplayName).FirstAsync(cancellationToken)),
                (await db.AppUsers.Where(x => x.Id == membership.UserId).Select(x => x.IsActive).FirstAsync(cancellationToken)),
                role.Code,
                role.Name,
                membership.CreatedAt));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> DeleteAsync(
        int warehouseId,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = currentUserService.GetRequiredUserId(User);
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                currentUserId,
                warehouseId,
                WarehousePermission.UsersManage,
                cancellationToken);

            var membership = await db.WarehouseUsers
                .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.UserId == userId, cancellationToken)
                ?? throw new InvalidOperationException("Пользователь не найден в выбранном складе.");

            db.WarehouseUsers.Remove(membership);
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
