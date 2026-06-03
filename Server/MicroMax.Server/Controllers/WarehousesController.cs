using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет справочником складов.
/// </summary>
[Route("api/warehouses")]
public sealed class WarehousesController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Warehouse>>> GetAsync() =>
        Ok(await Db.Warehouses.OrderBy(x => x.Name).ToListAsync());

    [HttpPost]
    public Task<ActionResult<Warehouse>> CreateAsync([FromBody] Warehouse item) =>
        CreateEntityAsync(item);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] Warehouse input)
    {
        var item = await Db.Warehouses.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = input.Name;
        item.Address = input.Address;
        await Db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<Warehouse>(id);
}
