using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

public sealed class WarehousePermissionService(MicroMaxDbContext db)
{
    public async Task<IReadOnlyList<int>> GetAccessibleWarehouseIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.User!.IsActive)
            .Select(x => x.WarehouseId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureAnyWarehouseAccessAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!await db.WarehouseUsers.AnyAsync(x => x.UserId == userId && x.User!.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("У пользователя нет доступа ни к одному складу.");
        }
    }

    public async Task EnsureWarehousePermissionAsync(
        int userId,
        int warehouseId,
        WarehousePermission permission,
        CancellationToken cancellationToken = default)
    {
        var roleCode = await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.WarehouseId == warehouseId && x.User!.IsActive)
            .Select(x => x.Role!.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleCode is null)
        {
            throw new InvalidOperationException("У пользователя нет доступа к выбранному складу.");
        }

        if (!RolePermissionMap.HasPermission(roleCode, permission))
        {
            throw new InvalidOperationException("Недостаточно прав для выполнения действия.");
        }
    }

    public async Task EnsureProductManagementAccessAsync(int userId, CancellationToken cancellationToken = default)
    {
        var hasPermission = await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.User!.IsActive)
            .AnyAsync(x => x.Role!.Code == SystemRoleCodes.Admin, cancellationToken);

        if (!hasPermission)
        {
            throw new InvalidOperationException("Только ADMIN может изменять глобальный каталог номенклатуры.");
        }
    }

    public async Task<int> EnsureOperationAccessAsync(
        int userId,
        int? sourceCellId,
        int? targetCellId,
        CancellationToken cancellationToken = default)
    {
        var sourceWarehouseId = sourceCellId is null
            ? (int?)null
            : await GetWarehouseIdForCellAsync(sourceCellId.Value, cancellationToken);
        var targetWarehouseId = targetCellId is null
            ? (int?)null
            : await GetWarehouseIdForCellAsync(targetCellId.Value, cancellationToken);

        var warehouseId = targetWarehouseId ?? sourceWarehouseId
            ?? throw new InvalidOperationException("Не удалось определить склад для операции.");

        if (sourceWarehouseId is not null && targetWarehouseId is not null && sourceWarehouseId != targetWarehouseId)
        {
            throw new InvalidOperationException("Складская операция должна выполняться в рамках одного склада.");
        }

        await EnsureWarehousePermissionAsync(userId, warehouseId, WarehousePermission.OperationsExecute, cancellationToken);
        return warehouseId;
    }

    public async Task<int> GetWarehouseIdForZoneAsync(int zoneId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.StorageZones
            .Where(x => x.Id == zoneId)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new InvalidOperationException("Зона хранения не найдена.")
            : warehouseId;
    }

    public async Task<int> GetWarehouseIdForCellAsync(int cellId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.StorageCells
            .Where(x => x.Id == cellId)
            .Select(x => x.StorageZone!.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new InvalidOperationException("Ячейка хранения не найдена.")
            : warehouseId;
    }

    public async Task<int> GetWarehouseIdForOperationAsync(int operationId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.WarehouseOperations
            .Where(x => x.Id == operationId)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new InvalidOperationException("Складская операция не найдена.")
            : warehouseId;
    }

    public async Task<Role> GetRequiredRoleAsync(string roleCode, CancellationToken cancellationToken = default)
    {
        var normalizedRoleCode = NormalizeRoleCode(roleCode);
        return await db.Roles.FirstOrDefaultAsync(x => x.Code == normalizedRoleCode, cancellationToken)
            ?? throw new InvalidOperationException("Указана неизвестная роль.");
    }

    public static string NormalizeRoleCode(string roleCode)
    {
        var normalizedRoleCode = roleCode.Trim().ToUpperInvariant();
        return normalizedRoleCode switch
        {
            SystemRoleCodes.Admin => SystemRoleCodes.Admin,
            SystemRoleCodes.Worker => SystemRoleCodes.Worker,
            SystemRoleCodes.Viewer => SystemRoleCodes.Viewer,
            _ => throw new InvalidOperationException("Поддерживаются только роли ADMIN, WORKER и VIEWER.")
        };
    }
}
