namespace MicroMax.Server.Api.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthUserWarehouseDto(
    int WarehouseId,
    string WarehouseName,
    string RoleCode,
    string RoleName);

public sealed record AuthUserDto(
    int Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<AuthUserWarehouseDto> Warehouses);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    AuthUserDto User);
