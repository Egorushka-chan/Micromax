using MicroMax.Server.Data;
using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Базовый контроллер с общими операциями для простых CRUD-маршрутов API.
/// </summary>
[ApiController]
public abstract class MicroMaxControllerBase(MicroMaxDbContext db) : ControllerBase
{
    protected MicroMaxDbContext Db => db;

    protected async Task<ActionResult<T>> CreateEntityAsync<T>(T item) where T : class
    {
        Db.Set<T>().Add(item);
        await Db.SaveChangesAsync();
        return Ok(item);
    }

    protected async Task<IActionResult> DeleteEntityAsync<T>(int id) where T : class
    {
        var item = await Db.Set<T>().FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        Db.Remove(item);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    protected static BadRequestObjectResult InvalidOperation(InvalidOperationException ex) =>
        new(new { error = ex.Message });
}
