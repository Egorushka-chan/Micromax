using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет ролями пользователей микросклада.
/// </summary>
[Route("api/roles")]
public sealed class RolesController(MicroMaxDbContext db) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserRole>>> GetAsync() =>
        Ok(await Db.UserRoles.OrderBy(x => x.Name).ToListAsync());

    [HttpPost]
    public Task<ActionResult<UserRole>> CreateAsync([FromBody] UserRole item) =>
        CreateEntityAsync(item);

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<UserRole>(id);
}
