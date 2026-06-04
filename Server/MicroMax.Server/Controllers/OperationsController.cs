using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Выполняет складские операции и возвращает журнал операций.
/// </summary>
[Authorize]
[Route("api/operations")]
public sealed class OperationsController(
    MicroMaxDbContext db,
    WarehouseOperationService service,
    CurrentUserService currentUserService,
    WarehousePermissionService warehousePermissionService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);
        var query = Db.WarehouseOperations
            .Include(x => x.Product)
            .Include(x => x.SourceCell)
            .Include(x => x.TargetCell)
            .Include(x => x.AppUser)
            .Where(x => warehouseIds.Contains(x.WarehouseId))
            .OrderByDescending(x => x.CreatedAt)
            .AsQueryable();

        if (from is not null)
        {
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.CreatedAt <= to);
        }

        return Ok(await query
            .Take(200)
            .Select(x => new
            {
                x.Id,
                x.WarehouseId,
                Type = x.Type.ToString(),
                ProductName = x.Product!.Name,
                SourceCell = x.SourceCell == null ? null : x.SourceCell.Code,
                TargetCell = x.TargetCell == null ? null : x.TargetCell.Code,
                x.AppUserId,
                PerformedBy = x.AppUser == null ? null : x.AppUser.DisplayName,
                x.Quantity,
                x.Comment,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken));
    }

    [HttpPost("receive")]
    public async Task<IActionResult> ReceiveAsync([FromBody] ReceiveRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureOperationAccessAsync(userId, null, request.TargetCellId, cancellationToken);
        return await RunOperationAsync(() => service.ReceiveAsync(request, userId));
    }

    [HttpPost("move")]
    public async Task<IActionResult> MoveAsync([FromBody] MoveRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureOperationAccessAsync(
            userId,
            request.SourceCellId,
            request.TargetCellId,
            cancellationToken);
        return await RunOperationAsync(() => service.MoveAsync(request, userId));
    }

    [HttpPost("write-off")]
    public async Task<IActionResult> WriteOffAsync([FromBody] WriteOffRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureOperationAccessAsync(userId, request.SourceCellId, null, cancellationToken);
        return await RunOperationAsync(() => service.WriteOffAsync(request, userId));
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustAsync([FromBody] AdjustRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        await warehousePermissionService.EnsureOperationAccessAsync(userId, null, request.TargetCellId, cancellationToken);
        return await RunOperationAsync(() => service.AdjustAsync(request, userId));
    }

    private static async Task<IActionResult> RunOperationAsync(Func<Task<WarehouseOperation>> action)
    {
        try
        {
            var operation = await action();
            return new OkObjectResult(new { operation.Id, operation.Type, operation.Quantity, operation.CreatedAt });
        }
        catch (InvalidOperationException ex)
        {
            return InvalidOperation(ex);
        }
    }
}
