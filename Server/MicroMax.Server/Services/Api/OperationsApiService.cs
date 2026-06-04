using MicroMax.Server.Api.Operations;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

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

    public async Task<WarehouseOperationResultResponse> WriteOffAsync(
        int userId,
        WriteOffRequest request,
        CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureOperationAccessAsync(userId, request.SourceCellId, null, cancellationToken);
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

    private static WarehouseOperationResultResponse ToResult(WarehouseOperation operation) =>
        new(operation.Id, operation.Type.ToString(), operation.Quantity, operation.CreatedAt);
}
