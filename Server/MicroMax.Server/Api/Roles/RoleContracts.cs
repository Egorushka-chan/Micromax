namespace MicroMax.Server.Api.Roles;

public sealed record RoleResponse(
    int Id,
    string Code,
    string Name);

public sealed record CreateRoleRequest(
    string Code,
    string Name);
