using System.Security.Claims;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

/// <summary>
/// Проверяет, что пользователь панели активен и имеет роль ADMIN хотя бы на одном складе.
/// </summary>
public sealed class AdminPanelAuthorizationHandler(MicroMaxDbContext db)
    : AuthorizationHandler<AdminPanelRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPanelRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var hasAdminAccess = await db.AppUsers
            .Where(x => x.Id == userId && x.IsActive)
            .AnyAsync(x => x.WarehouseUsers.Any(y => y.Role!.Code == SystemRoleCodes.Admin));

        if (hasAdminAccess)
        {
            context.Succeed(requirement);
        }
    }
}
