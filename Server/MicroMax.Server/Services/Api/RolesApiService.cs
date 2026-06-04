using MicroMax.Server.Api.Roles;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class RolesApiService(
    Data.MicroMaxDbContext db)
{
    public async Task<IReadOnlyList<RoleResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await db.Roles
            .OrderBy(x => x.Name)
            .Select(x => new RoleResponse(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedCode = WarehousePermissionService.NormalizeRoleCode(request.Code);
        if (await db.Roles.AnyAsync(x => x.Code == normalizedCode, cancellationToken))
        {
            throw new ApiConflictException("Такая техническая роль уже существует.");
        }

        var role = new Role
        {
            Code = normalizedCode,
            Name = request.Name.Trim()
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        return new RoleResponse(role.Id, role.Code, role.Name);
    }

    public async Task DeleteAsync(int currentUserId, int roleId, CancellationToken cancellationToken = default)
    {
        var hasAdminMembership = await db.WarehouseUsers
            .AnyAsync(x => x.UserId == currentUserId && x.Role!.Code == SystemRoleCodes.Admin, cancellationToken);

        if (!hasAdminMembership)
        {
            throw new ApiForbiddenException("Удаление ролей доступно только ADMIN.");
        }

        var role = await db.Roles.FindAsync([roleId], cancellationToken)
            ?? throw new ApiNotFoundException("Роль не найдена.");

        if (role.Code is SystemRoleCodes.Admin or SystemRoleCodes.Worker or SystemRoleCodes.Viewer)
        {
            throw new ApiConflictException("Базовые роли ADMIN, WORKER и VIEWER не удаляются.");
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }
}
