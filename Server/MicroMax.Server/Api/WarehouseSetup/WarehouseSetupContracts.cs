using MicroMax.Server.Api.Warehouses;

namespace MicroMax.Server.Api.WarehouseSetup;

public sealed record WarehouseSetupTemplateResponse(
    string Code,
    string Name,
    string Description,
    int ZonesCount,
    int CellsCount);

public sealed record CreateWarehouseFromTemplateRequest(
    string Name,
    string? Address,
    string TemplateCode);

public sealed record WarehouseSetupResponse(
    int WarehouseId,
    string WarehouseName,
    string RoleCode,
    string RoleName,
    int ZonesCreated,
    int CellsCreated,
    WarehouseStructureResponse Structure);
