using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет номенклатурой и местонахождением товаров.
/// </summary>
[Route("api/products")]
public sealed class ProductsController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAsync() =>
        Ok(await Db.Products.OrderBy(x => x.Name).ToListAsync());

    [HttpGet("{id:int}/locations")]
    public async Task<IActionResult> GetLocationsAsync(int id) =>
        Ok(await Db.StockBalances
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.ProductId == id && x.Quantity > 0)
            .Select(x => new
            {
                CellId = x.StorageCellId,
                CellCode = x.StorageCell!.Code,
                ZoneCode = x.StorageCell.StorageZone!.Code,
                x.Quantity
            })
            .ToListAsync());

    [HttpPost]
    public Task<ActionResult<Product>> CreateAsync([FromBody] Product item) =>
        CreateEntityAsync(item);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] Product input)
    {
        var item = await Db.Products.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        item.Sku = input.Sku;
        item.Name = input.Name;
        item.Unit = input.Unit;
        item.MinQuantity = input.MinQuantity;
        await Db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<Product>(id);
}
