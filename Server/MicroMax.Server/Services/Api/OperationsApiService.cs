using MicroMax.Server.Api.Operations;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace MicroMax.Server.Services.Api;

public sealed class OperationsApiService(
    Data.MicroMaxDbContext db,
    WarehouseOperationService warehouseOperationService,
    WarehousePermissionService warehousePermissionService)
{
    public async Task<IReadOnlyList<WarehouseOperationResponse>> GetAsync(
        int userId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        var query = db.WarehouseOperations
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        if (from is not null)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        return await query
            .Take(200)
            .Select(ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseOperationResponse>> GetForWarehouseAsync(
        int userId,
        int warehouseId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.OperationsReadJournal,
            cancellationToken);

        var query = db.WarehouseOperations
            .Where(x => x.WarehouseId == warehouseId)
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        if (from is not null)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        return await query
            .Take(200)
            .Select(ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseOperationResultResponse> ReceiveAsync(
        int userId,
        ReceiveRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureOperationAccessAsync(userId, null, request.TargetCellId, cancellationToken);
        var operation = await warehouseOperationService.ReceiveAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> ReceiveAsync(
        int userId,
        int warehouseId,
        ReceiveRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.OperationsExecute,
            cancellationToken);
        await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
            warehouseId,
            null,
            request.TargetCellId,
            cancellationToken);
        var operation = await warehouseOperationService.ReceiveAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> MoveAsync(
        int userId,
        MoveRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureOperationAccessAsync(
            userId,
            request.SourceCellId,
            request.TargetCellId,
            cancellationToken);
        var operation = await warehouseOperationService.MoveAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> MoveAsync(
        int userId,
        int warehouseId,
        MoveRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.OperationsExecute,
            cancellationToken);
        await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
            warehouseId,
            request.SourceCellId,
            request.TargetCellId,
            cancellationToken);
        var operation = await warehouseOperationService.MoveAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> WriteOffAsync(
        int userId,
        WriteOffRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureOperationAccessAsync(userId, request.SourceCellId, null, cancellationToken);
        var operation = await warehouseOperationService.WriteOffAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> WriteOffAsync(
        int userId,
        int warehouseId,
        WriteOffRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.OperationsExecute,
            cancellationToken);
        await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
            warehouseId,
            request.SourceCellId,
            null,
            cancellationToken);
        var operation = await warehouseOperationService.WriteOffAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> AdjustAsync(
        int userId,
        AdjustRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureOperationAccessAsync(userId, null, request.TargetCellId, cancellationToken);
        var operation = await warehouseOperationService.AdjustAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    public async Task<WarehouseOperationResultResponse> AdjustAsync(
        int userId,
        int warehouseId,
        AdjustRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.OperationsExecute,
            cancellationToken);
        await warehousePermissionService.EnsureWarehouseMatchesCellsAsync(
            warehouseId,
            null,
            request.TargetCellId,
            cancellationToken);
        var operation = await warehouseOperationService.AdjustAsync(request, userId, cancellationToken);
        return ToResult(operation);
    }

    private static WarehouseOperationResultResponse ToResult(WarehouseOperation operation) =>
        new(operation.Id, operation.Type.ToString(), operation.Quantity, operation.CreatedAt);

    private static Expression<Func<WarehouseOperation, WarehouseOperationResponse>> ToResponse() =>
        x => new WarehouseOperationResponse(
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
            x.CreatedAt);
}
