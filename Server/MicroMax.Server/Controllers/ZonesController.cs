using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет зоны хранения.
/// </summary>
[Route("api/zones")]
public sealed class ZonesController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StorageZone>>> GetAsync() =>
        Ok(await Db.StorageZones
            .Include(x => x.Warehouse)
            .OrderBy(x => x.Code)
            .ToListAsync());

    [HttpPost]
    public Task<ActionResult<StorageZone>> CreateAsync([FromBody] StorageZone item) =>
        CreateEntityAsync(item);

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<StorageZone>(id);
}
