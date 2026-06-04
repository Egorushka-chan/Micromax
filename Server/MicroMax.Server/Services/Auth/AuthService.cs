using MicroMax.Server.Api.Auth;
using MicroMax.Server.Configuration;
using MicroMax.Server.Data;
using MicroMax.Server.Infrastructure.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MicroMax.Server.Services.Auth;

public sealed class AuthService(
    MicroMaxDbContext db,
    IPasswordHasher<MicroMax.Server.Models.AppUser> passwordHasher,
    JwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
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

        var user = new Models.AppUser
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.AppUsers.FirstOrDefaultAsync(x => x.Email == email, cancellationToken)
            ?? throw new ApiUnauthorizedException("Пользователь с таким email не найден.");

        if (!user.IsActive)
        {
            throw new ApiForbiddenException("Учетная запись пользователя отключена.");
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new ApiUnauthorizedException("Неверный пароль.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = JwtTokenService.ComputeTokenHash(request.RefreshToken);
        var refreshToken = await db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new ApiUnauthorizedException("Refresh token не найден.");

        if (refreshToken.RevokedAt is not null || refreshToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ApiUnauthorizedException("Refresh token больше не действителен.");
        }

        if (refreshToken.User is null || !refreshToken.User.IsActive)
        {
            throw new ApiForbiddenException("Учетная запись пользователя отключена.");
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(refreshToken.User, cancellationToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = JwtTokenService.ComputeTokenHash(request.RefreshToken);
        var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (refreshToken is null || refreshToken.RevokedAt is not null)
        {
            return;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserDto> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.AppUsers
            .Where(x => x.Id == userId)
            .Select(x => new AuthUserDto(
                x.Id,
                x.Email,
                x.DisplayName,
                x.IsActive,
                x.WarehouseUsers
                    .OrderBy(y => y.Warehouse!.Name)
                    .Select(y => new AuthUserWarehouseDto(
                        y.WarehouseId,
                        y.Warehouse!.Name,
                        y.Role!.Code,
                        y.Role.Name))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiNotFoundException("Пользователь не найден.");

        return user;
    }

    private async Task<AuthResponse> IssueTokensAsync(Models.AppUser user, CancellationToken cancellationToken)
    {
        var (accessToken, accessTokenExpiresAt) = jwtTokenService.CreateAccessToken(user);
        var refreshTokenValue = jwtTokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new Models.RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenService.ComputeTokenHash(refreshTokenValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenLifetimeDays),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var userDto = await GetUserProfileAsync(user.Id, cancellationToken);
        return new AuthResponse(accessToken, accessTokenExpiresAt, refreshTokenValue, userDto);
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
