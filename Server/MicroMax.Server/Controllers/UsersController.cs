using MicroMax.Server.Api.Users;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Возвращает и изменяет пользователей системы.
/// </summary>
[Authorize]
[Route("api/users")]
public sealed class UsersController(
    MicroMaxDbContext db,
    AuthService authService,
    CurrentUserService currentUserService,
    IPasswordHasher<AppUser> passwordHasher) : MicroMaxControllerBase(db)
{
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(User);
        var profile = await authService.GetUserProfileAsync(userId, cancellationToken);

        return Ok(new CurrentUserResponse(
            profile.Id,
            profile.Email,
            profile.DisplayName,
            profile.IsActive,
            profile.Warehouses
                .Select(x => new CurrentUserWarehouseResponse(
                    x.WarehouseId,
                    x.WarehouseName,
                    x.RoleCode,
                    x.RoleName))
                .ToList()));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppUser>>> GetAsync() =>
        Ok(await Db.AppUsers
            .OrderBy(x => x.DisplayName)
            .ToListAsync());

    [HttpPost]
    public async Task<ActionResult<AppUser>> CreateAsync([FromBody] AppUser item)
    {
        item.Email = item.Email.Trim().ToLowerInvariant();
        item.PasswordHash = passwordHasher.HashPassword(item, item.PasswordHash);
        item.CreatedAt = DateTimeOffset.UtcNow;
        item.IsActive = true;
        return await CreateEntityAsync(item);
    }

    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteAsync(int id) =>
        DeleteEntityAsync<AppUser>(id);
}
