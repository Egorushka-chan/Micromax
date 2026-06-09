using MicroMax.Server.Api.Cells;
using MicroMax.Server.Api.Operations;
using MicroMax.Server.Api.Products;
using MicroMax.Server.Api.Stocks;
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
        var warehouses = await GetMyAsync(userId, cancellationToken);
        return warehouses
            .Select(x => new WarehouseResponse(x.WarehouseId, x.Name, x.Address))
            .ToList();
    }

    public async Task<IReadOnlyList<UserWarehouseResponse>> GetMyAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.User!.IsActive)
            .OrderBy(x => x.Warehouse!.Name)
            .Select(x => new UserWarehouseResponse(
                x.WarehouseId,
                x.Warehouse!.Name,
                x.Warehouse.Address,
                x.Role!.Code,
                x.Role.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDetailsResponse> GetByIdAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var context = await warehousePermissionService.GetRequiredUserWarehouseContextAsync(userId, warehouseId, cancellationToken);
        var warehouse = await db.Warehouses
            .Where(x => x.Id == warehouseId)
            .Select(x => new { x.Id, x.Name, x.Address })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiNotFoundException("Склад не найден.");

        return new WarehouseDetailsResponse(
            warehouse.Id,
            warehouse.Name,
            warehouse.Address,
            context.RoleCode,
            context.RoleName);
    }

    public async Task<WarehouseStructureResponse> GetStructureAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetByIdAsync(userId, warehouseId, cancellationToken);

        var zones = await db.StorageZones
            .Where(x => x.WarehouseId == warehouseId)
            .OrderBy(x => x.Code)
            .Select(x => new WarehouseStructureZoneResponse(
                x.Id,
                x.Code,
                x.Name,
                x.Cells
                    .OrderBy(cell => cell.Code)
                    .Select(cell => new WarehouseStructureCellResponse(cell.Id, cell.Code, cell.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new WarehouseStructureResponse(
            details.WarehouseId,
            details.Name,
            details.Address,
            details.RoleCode,
            details.RoleName,
            zones);
    }

    public async Task<WarehouseSnapshotResponse> GetSnapshotAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehouseAccessAsync(userId, warehouseId, cancellationToken);
        var warehouse = await GetByIdAsync(userId, warehouseId, cancellationToken);

        var products = await db.Products
            .OrderBy(x => x.Name)
            .Select(x => new ProductResponse(x.Id, x.Sku, x.Name, x.Unit, x.MinQuantity))
            .ToListAsync(cancellationToken);

        var cells = await db.StorageCells
            .Where(x => x.StorageZone!.WarehouseId == warehouseId)
            .OrderBy(x => x.Code)
            .Select(x => new StorageCellResponse(
                x.Id,
                x.StorageZoneId,
                warehouseId,
                x.Code,
                x.Name,
                x.StorageZone!.Code,
                x.StorageZone.Warehouse!.Name))
            .ToListAsync(cancellationToken);

        var stocks = await db.StockBalances
            .Where(x => x.Quantity > 0 && x.StorageCell!.StorageZone!.WarehouseId == warehouseId)
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

        var operations = await db.WarehouseOperations
            .Where(x => x.WarehouseId == warehouseId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new WarehouseOperationResponse(
                x.Id,
                x.WarehouseId,
                x.Type.ToString(),
                x.Product!.Name,
                x.SourceCell == null ? null : x.SourceCell.Code,
                x.TargetCell == null ? null : x.TargetCell.Code,
                x.AppUserId,
                x.AppUser == null ? null : x.AppUser.DisplayName,
                x.Quantity,
                x.Comment,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new WarehouseSnapshotResponse(warehouse, products, cells, stocks, operations);
    }

    public async Task<WarehouseResponse> CreateAsync(
        int userId,
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(warehouseName))
        {
            throw new ApiValidationException("Укажите название склада.");
        }

        var adminRole = await warehousePermissionService.GetRequiredRoleAsync(SystemRoleCodes.Admin, cancellationToken);
        var warehouse = new Warehouse
        {
            Name = warehouseName,
            Address = NormalizeOptional(request.Address)
        };

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

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

        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }

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
