using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Работает со складскими ячейками и их содержимым.
/// </summary>
[Route("api/cells")]
public sealed class CellsController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> GetAsync() =>
        Ok(await Db.StorageCells
            .Include(x => x.StorageZone)
            .ThenInclude(x => x!.Warehouse)
            .OrderBy(x => x.Code)
            .ToListAsync());

    [HttpGet("{id:int}/contents")]
    public async Task<IActionResult> GetContentsAsync(int id) =>
        Ok(await Db.StockBalances
            .Include(x => x.Product)
            .Where(x => x.StorageCellId == id && x.Quantity > 0)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product!.Name,
                x.Product.Sku,
                x.Quantity,
                x.Product.Unit
            })
            .ToListAsync());

    [HttpPost]
    public Task<ActionResult<StorageCell>> CreateAsync([FromBody] StorageCell item) =>
        CreateEntityAsync(item);

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<StorageCell>(id);
}
