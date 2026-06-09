using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

public sealed record WarehouseUserContext(
    int WarehouseId,
    string WarehouseName,
    string RoleCode,
    string RoleName);

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
            throw new ApiForbiddenException("У пользователя нет доступа ни к одному складу.");
        }
    }

    public async Task<WarehouseUserContext> GetRequiredUserWarehouseContextAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var context = await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.WarehouseId == warehouseId && x.User!.IsActive)
            .Select(x => new WarehouseUserContext(
                x.WarehouseId,
                x.Warehouse!.Name,
                x.Role!.Code,
                x.Role.Name))
            .FirstOrDefaultAsync(cancellationToken);

        return context
            ?? throw new ApiForbiddenException("У пользователя нет доступа к выбранному складу.");
    }

    public Task EnsureWarehouseAccessAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default) =>
        EnsureWarehousePermissionAsync(userId, warehouseId, WarehousePermission.WarehouseRead, cancellationToken);

    public async Task EnsureWarehousePermissionAsync(
        int userId,
        int warehouseId,
        WarehousePermission permission,
        CancellationToken cancellationToken = default)
    {
        var context = await GetRequiredUserWarehouseContextAsync(userId, warehouseId, cancellationToken);

        if (!RolePermissionMap.HasPermission(context.RoleCode, permission))
        {
            throw new ApiForbiddenException("Недостаточно прав для выполнения действия.");
        }
    }

    public async Task EnsureProductManagementAccessAsync(int userId, CancellationToken cancellationToken = default)
    {
        var hasPermission = await db.WarehouseUsers
            .Where(x => x.UserId == userId && x.User!.IsActive)
            .AnyAsync(x => x.Role!.Code == SystemRoleCodes.Admin, cancellationToken);

        if (!hasPermission)
        {
            throw new ApiForbiddenException("Только ADMIN может изменять глобальный каталог номенклатуры.");
        }
    }

    public Task EnsureProductManagementAccessAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken = default) =>
        EnsureWarehousePermissionAsync(userId, warehouseId, WarehousePermission.ProductManage, cancellationToken);

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
            ?? throw new ApiValidationException("Не удалось определить склад для операции.");

        if (sourceWarehouseId is not null && targetWarehouseId is not null && sourceWarehouseId != targetWarehouseId)
        {
            throw new ApiConflictException("Складская операция должна выполняться в рамках одного склада.");
        }

        await EnsureWarehousePermissionAsync(userId, warehouseId, WarehousePermission.OperationsExecute, cancellationToken);
        return warehouseId;
    }

    public async Task EnsureWarehouseMatchesCellsAsync(
        int warehouseId,
        int? sourceCellId,
        int? targetCellId,
        CancellationToken cancellationToken = default)
    {
        if (sourceCellId is not null)
        {
            await EnsureCellBelongsToWarehouseAsync(warehouseId, sourceCellId.Value, cancellationToken);
        }

        if (targetCellId is not null)
        {
            await EnsureCellBelongsToWarehouseAsync(warehouseId, targetCellId.Value, cancellationToken);
        }
    }

    public async Task EnsureCellBelongsToWarehouseAsync(
        int warehouseId,
        int cellId,
        CancellationToken cancellationToken = default)
    {
        var actualWarehouseId = await GetWarehouseIdForCellAsync(cellId, cancellationToken);
        if (actualWarehouseId != warehouseId)
        {
            throw new ApiConflictException("Выбранная ячейка относится к другому складу.");
        }
    }

    public async Task<int> GetWarehouseIdForZoneAsync(int zoneId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.StorageZones
            .Where(x => x.Id == zoneId)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new ApiNotFoundException("Зона хранения не найдена.")
            : warehouseId;
    }

    public async Task<int> GetWarehouseIdForCellAsync(int cellId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.StorageCells
            .Where(x => x.Id == cellId)
            .Select(x => x.StorageZone!.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new ApiNotFoundException("Ячейка хранения не найдена.")
            : warehouseId;
    }

    public async Task<int> GetWarehouseIdForOperationAsync(int operationId, CancellationToken cancellationToken = default)
    {
        var warehouseId = await db.WarehouseOperations
            .Where(x => x.Id == operationId)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseId == 0
            ? throw new ApiNotFoundException("Складская операция не найдена.")
            : warehouseId;
    }

    public async Task<Role> GetRequiredRoleAsync(string roleCode, CancellationToken cancellationToken = default)
    {
        var normalizedRoleCode = NormalizeRoleCode(roleCode);
        return await db.Roles.FirstOrDefaultAsync(x => x.Code == normalizedRoleCode, cancellationToken)
            ?? throw new ApiNotFoundException("Указанная роль не найдена.");
    }

    public static string NormalizeRoleCode(string roleCode)
    {
        var normalizedRoleCode = roleCode.Trim().ToUpperInvariant();
        return normalizedRoleCode switch
        {
            SystemRoleCodes.Admin => SystemRoleCodes.Admin,
            SystemRoleCodes.Worker => SystemRoleCodes.Worker,
            SystemRoleCodes.Viewer => SystemRoleCodes.Viewer,
            _ => throw new ApiValidationException("Поддерживаются только роли ADMIN, WORKER и VIEWER.")
        };
    }
}
