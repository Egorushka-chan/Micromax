namespace MicroMax.Server.Api.Warehouses;

public sealed record WarehouseUserResponse(
    int UserId,
    string Email,
    string DisplayName,
    bool IsActive,
    string RoleCode,
    string RoleName,
    DateTimeOffset CreatedAt);

public sealed record AddWarehouseUserRequest(
    string Email,
    string RoleCode);

public sealed record UpdateWarehouseUserRoleRequest(
    string RoleCode);
