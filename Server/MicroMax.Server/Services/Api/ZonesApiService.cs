using MicroMax.Server.Api.Zones;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class ZonesApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<StorageZoneResponse>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return await db.StorageZones
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .OrderBy(x => x.Code)
            .Select(x => new StorageZoneResponse(
                x.Id,
                x.WarehouseId,
                x.Code,
                x.Name,
                x.Warehouse!.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<StorageZoneResponse> CreateAsync(
        int userId,
        CreateStorageZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await db.Warehouses.FindAsync([request.WarehouseId], cancellationToken)
            ?? throw new ApiNotFoundException("Склад не найден.");

        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            request.WarehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        var code = request.Code.Trim();
        if (await db.StorageZones.AnyAsync(
                x => x.WarehouseId == request.WarehouseId && x.Code == code,
                cancellationToken))
        {
            throw new ApiConflictException("Зона с таким кодом уже существует в выбранном складе.");
        }

        var zone = new StorageZone
        {
            WarehouseId = request.WarehouseId,
            Code = code,
            Name = request.Name.Trim()
        };

        db.StorageZones.Add(zone);
        await db.SaveChangesAsync(cancellationToken);

        return new StorageZoneResponse(zone.Id, zone.WarehouseId, zone.Code, zone.Name, warehouse.Name);
    }

    public async Task DeleteAsync(int userId, int zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await db.StorageZones
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == zoneId, cancellationToken)
            ?? throw new ApiNotFoundException("Зона хранения не найдена.");

        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            zone.WarehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        db.StorageZones.Remove(zone);
        await db.SaveChangesAsync(cancellationToken);
    }
}
