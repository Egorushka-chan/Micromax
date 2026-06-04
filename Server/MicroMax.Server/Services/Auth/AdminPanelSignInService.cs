using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Auth;

/// <summary>
/// Выполняет проверку учетных данных для входа в веб-панель администратора.
/// </summary>
public sealed class AdminPanelSignInService(
    MicroMaxDbContext db,
    IPasswordHasher<AppUser> passwordHasher)
{
    public async Task<AppUser?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var user = await db.AppUsers
            .Include(x => x.WarehouseUsers)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return user.WarehouseUsers.Any(x => x.Role?.Code == SystemRoleCodes.Admin)
            ? user
            : null;
    }
}
