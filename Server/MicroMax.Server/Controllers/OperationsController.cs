using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Выполняет складские операции и возвращает журнал операций.
/// </summary>
[Route("api/operations")]
public sealed class OperationsController(MicroMaxDbContext db, WarehouseOperationService service) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        var query = Db.WarehouseOperations
            .Include(x => x.Product)
            .Include(x => x.SourceCell)
            .Include(x => x.TargetCell)
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
                Type = x.Type.ToString(),
                ProductName = x.Product!.Name,
                SourceCell = x.SourceCell == null ? null : x.SourceCell.Code,
                TargetCell = x.TargetCell == null ? null : x.TargetCell.Code,
                x.Quantity,
                x.Comment,
                x.CreatedAt
            })
            .ToListAsync());
    }

    [HttpPost("receive")]
    public Task<IActionResult> ReceiveAsync([FromBody] ReceiveRequest request) =>
        RunOperationAsync(() => service.ReceiveAsync(request));

    [HttpPost("move")]
    public Task<IActionResult> MoveAsync([FromBody] MoveRequest request) =>
        RunOperationAsync(() => service.MoveAsync(request));

    [HttpPost("write-off")]
    public Task<IActionResult> WriteOffAsync([FromBody] WriteOffRequest request) =>
        RunOperationAsync(() => service.WriteOffAsync(request));

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
