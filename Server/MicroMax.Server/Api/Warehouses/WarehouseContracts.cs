namespace MicroMax.Server.Api.Warehouses;

public sealed record WarehouseResponse(
    int Id,
    string Name,
    string? Address);

public sealed record CreateWarehouseRequest(
    string Name,
    string? Address);

public sealed record UpdateWarehouseRequest(
    string Name,
    string? Address);
