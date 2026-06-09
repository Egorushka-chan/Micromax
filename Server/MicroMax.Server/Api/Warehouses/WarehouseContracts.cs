using MicroMax.Server.Api.Cells;
using MicroMax.Server.Api.Operations;
using MicroMax.Server.Api.Products;
using MicroMax.Server.Api.Stocks;

namespace MicroMax.Server.Api.Warehouses;

public sealed record WarehouseResponse(
    int Id,
    string Name,
    string? Address);

public sealed record UserWarehouseResponse(
    int WarehouseId,
    string Name,
    string? Address,
    string RoleCode,
    string RoleName);

public sealed record WarehouseDetailsResponse(
    int WarehouseId,
    string Name,
    string? Address,
    string RoleCode,
    string RoleName);

public sealed record WarehouseStructureCellResponse(
    int CellId,
    string Code,
    string Name);

public sealed record WarehouseStructureZoneResponse(
    int ZoneId,
    string Code,
    string Name,
    IReadOnlyList<WarehouseStructureCellResponse> Cells);

public sealed record WarehouseStructureResponse(
    int WarehouseId,
    string Name,
    string? Address,
    string RoleCode,
    string RoleName,
    IReadOnlyList<WarehouseStructureZoneResponse> Zones);

public sealed record WarehouseSnapshotResponse(
    WarehouseDetailsResponse Warehouse,
    IReadOnlyList<ProductResponse> Products,
    IReadOnlyList<StorageCellResponse> Cells,
    IReadOnlyList<StockBalanceResponse> Stocks,
    IReadOnlyList<WarehouseOperationResponse> Operations);

public sealed record CreateWarehouseRequest(
    string Name,
    string? Address);

public sealed record UpdateWarehouseRequest(
    string Name,
    string? Address);
