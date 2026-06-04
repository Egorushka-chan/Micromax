using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Управляет ролями пользователей микросклада.
/// </summary>
[Authorize]
[Route("api/roles")]
public sealed class RolesController(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : MicroMaxControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Role>>> GetAsync() =>
        Ok(await Db.Roles.OrderBy(x => x.Name).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<Role>> CreateAsync([FromBody] Role item)
    {
        item.Code = WarehousePermissionService.NormalizeRoleCode(item.Code);
        if (await Db.Roles.AnyAsync(x => x.Code == item.Code))
        {
            return BadRequest(new { error = "Такая техническая роль уже существует." });
        }

        return await CreateEntityAsync(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var currentUserId = currentUserService.GetRequiredUserId(User);
        var adminWarehouseId = await Db.WarehouseUsers
            .Where(x => x.UserId == currentUserId && x.Role!.Code == SystemRoleCodes.Admin)
            .Select(x => x.WarehouseId)
            .FirstOrDefaultAsync();

        if (adminWarehouseId == 0)
        {
            return BadRequest(new { error = "Удаление ролей доступно только ADMIN." });
        }

        var item = await Db.Roles.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        if (item.Code is SystemRoleCodes.Admin or SystemRoleCodes.Worker or SystemRoleCodes.Viewer)
        {
            return BadRequest(new { error = "Базовые роли ADMIN, WORKER и VIEWER не удаляются." });
        }

        Db.Remove(item);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
