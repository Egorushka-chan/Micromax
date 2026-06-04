namespace MicroMax.Server.Api.Users;

public sealed record CurrentUserResponse(
    int Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<CurrentUserWarehouseResponse> Warehouses);

public sealed record CurrentUserWarehouseResponse(
    int WarehouseId,
    string WarehouseName,
    string RoleCode,
    string RoleName);
