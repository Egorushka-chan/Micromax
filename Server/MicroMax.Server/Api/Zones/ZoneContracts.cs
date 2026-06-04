namespace MicroMax.Server.Api.Zones;

public sealed record StorageZoneResponse(
    int Id,
    int WarehouseId,
    string Code,
    string Name,
    string WarehouseName);

public sealed record CreateStorageZoneRequest(
    int WarehouseId,
    string Code,
    string Name);
