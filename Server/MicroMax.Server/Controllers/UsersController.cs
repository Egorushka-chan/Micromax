using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет пользователей системы.
/// </summary>
[Route("api/users")]
public sealed class UsersController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAsync() =>
        Ok(await Db.AppUsers
            .Include(x => x.UserRole)
            .OrderBy(x => x.DisplayName)
            .ToListAsync());

    [HttpPost]
    public Task<ActionResult<AppUser>> CreateAsync([FromBody] AppUser item) =>
        CreateEntityAsync(item);

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<AppUser>(id);
}
