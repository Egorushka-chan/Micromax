using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class WarehouseUsersApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<WarehouseUserResponse>> GetAsync(
        int currentUserId,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            currentUserId,
            warehouseId,
            WarehousePermission.UsersManage,
            cancellationToken);

        return await db.WarehouseUsers
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
    }

    public async Task<WarehouseUserResponse> CreateAsync(
        int currentUserId,
        int warehouseId,
        AddWarehouseUserRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            currentUserId,
            warehouseId,
            WarehousePermission.UsersManage,
            cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var targetUser = await db.AppUsers.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь с указанным email не найден.");
        var role = await warehousePermissionService.GetRequiredRoleAsync(request.RoleCode, cancellationToken);

        if (await db.WarehouseUsers.AnyAsync(
                x => x.WarehouseId == warehouseId && x.UserId == targetUser.Id,
                cancellationToken))
        {
            throw new ApiConflictException("Пользователь уже добавлен в этот склад.");
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

        return new WarehouseUserResponse(
            targetUser.Id,
            targetUser.Email,
            targetUser.DisplayName,
            targetUser.IsActive,
            role.Code,
            role.Name,
            membership.CreatedAt);
    }

    public async Task<WarehouseUserResponse> UpdateRoleAsync(
        int currentUserId,
        int warehouseId,
        int userId,
        UpdateWarehouseUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            currentUserId,
            warehouseId,
            WarehousePermission.UsersManage,
            cancellationToken);

        var membership = await db.WarehouseUsers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.UserId == userId, cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь не найден в выбранном складе.");

        var role = await warehousePermissionService.GetRequiredRoleAsync(request.RoleCode, cancellationToken);
        membership.RoleId = role.Id;
        await db.SaveChangesAsync(cancellationToken);

        return new WarehouseUserResponse(
            membership.UserId,
            membership.User!.Email,
            membership.User.DisplayName,
            membership.User.IsActive,
            role.Code,
            role.Name,
            membership.CreatedAt);
    }

    public async Task DeleteAsync(
        int currentUserId,
        int warehouseId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            currentUserId,
            warehouseId,
            WarehousePermission.UsersManage,
            cancellationToken);

        var membership = await db.WarehouseUsers
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.UserId == userId, cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь не найден в выбранном складе.");

        db.WarehouseUsers.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
    }
}
