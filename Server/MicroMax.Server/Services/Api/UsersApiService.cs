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
    IPasswordHasher<AppUser> passwordHasher)
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
        var email = NormalizeEmail(request.Email);
        var displayName = request.DisplayName.Trim();
        ValidatePassword(request.Password);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ApiValidationException("Имя пользователя не должно быть пустым.");
        }

        if (await db.AppUsers.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new ApiConflictException("Пользователь с таким email уже существует.");
        }

        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.Email, user.DisplayName, user.IsActive, user.CreatedAt);
    }

    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.AppUsers.FindAsync([userId], cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь не найден.");

        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            throw new ApiValidationException("Укажите корректный email.");
        }

        return normalizedEmail;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ApiValidationException("Пароль должен содержать не менее 8 символов.");
        }
    }
}
