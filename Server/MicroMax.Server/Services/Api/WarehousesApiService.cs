using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class WarehousesApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<WarehouseResponse>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return await db.Warehouses
            .Where(x => warehouseIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseResponse(x.Id, x.Name, x.Address))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseResponse> CreateAsync(
        int userId,
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminRole = await warehousePermissionService.GetRequiredRoleAsync(SystemRoleCodes.Admin, cancellationToken);
        var warehouse = new Warehouse
        {
            Name = request.Name.Trim(),
            Address = NormalizeOptional(request.Address)
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouse.Id,
            UserId = userId,
            RoleId = adminRole.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new WarehouseResponse(warehouse.Id, warehouse.Name, warehouse.Address);
    }

    public async Task<WarehouseResponse> UpdateAsync(
        int userId,
        int warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await db.Warehouses.FindAsync([warehouseId], cancellationToken)
            ?? throw new ApiNotFoundException("Склад не найден.");

        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        warehouse.Name = request.Name.Trim();
        warehouse.Address = NormalizeOptional(request.Address);
        await db.SaveChangesAsync(cancellationToken);

        return new WarehouseResponse(warehouse.Id, warehouse.Name, warehouse.Address);
    }

    public async Task DeleteAsync(int userId, int warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouse = await db.Warehouses.FindAsync([warehouseId], cancellationToken)
            ?? throw new ApiNotFoundException("Склад не найден.");

        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
