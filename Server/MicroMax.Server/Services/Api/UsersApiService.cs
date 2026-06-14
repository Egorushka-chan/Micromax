using MicroMax.Server.Api.Users;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class UsersApiService(
    Data.MicroMaxDbContext db,
    AuthService authService,
    UserAccountService userAccountService)
{
    public async Task<CurrentUserResponse> GetCurrentAsync(int userId, CancellationToken cancellationToken = default)
    {
        var profile = await authService.GetUserProfileAsync(userId, cancellationToken);

        return new CurrentUserResponse(
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
                .ToList());
    }

    public async Task<IReadOnlyList<UserResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await db.AppUsers
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserResponse(x.Id, x.Email, x.DisplayName, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userAccountService.CreateAsync(
            new CreateUserAccountRequest(
                request.Email,
                request.Password,
                request.DisplayName,
                CanAccessWebPanel: false),
            cancellationToken);

        return new UserResponse(user.Id, user.Email, user.DisplayName, user.IsActive, user.CreatedAt);
    }

    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.AppUsers.FindAsync([userId], cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь не найден.");

        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
