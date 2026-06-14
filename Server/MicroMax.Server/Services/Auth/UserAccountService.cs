using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

/// <summary>
/// Создаёт учётные записи пользователей с едиными правилами валидации и хеширования пароля.
/// </summary>
public sealed class UserAccountService(
    MicroMaxDbContext db,
    IPasswordHasher<AppUser> passwordHasher)
{
    public async Task<AppUser> CreateAsync(
        CreateUserAccountRequest request,
        CancellationToken cancellationToken = default)
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
            IsActive = true,
            CanAccessWebPanel = request.CanAccessWebPanel
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user;
    }

    public static string NormalizeEmail(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            throw new ApiValidationException("Укажите корректный email.");
        }

        return normalizedEmail;
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ApiValidationException("Пароль должен содержать не менее 8 символов.");
        }
    }
}

public sealed record CreateUserAccountRequest(
    string Email,
    string Password,
    string DisplayName,
    bool CanAccessWebPanel);
