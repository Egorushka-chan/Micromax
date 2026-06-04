namespace MicroMax.Server.Api.Cells;

public sealed record StorageCellResponse(
    int Id,
    int StorageZoneId,
    int WarehouseId,
    string Code,
    string Name,
    string ZoneCode,
    string WarehouseName);

public sealed record CreateStorageCellRequest(
    int StorageZoneId,
    string Code,
    string Name);

public sealed record CellContentsItemResponse(
    int ProductId,
    string ProductName,
    string Sku,
    decimal Quantity,
    string Unit);
