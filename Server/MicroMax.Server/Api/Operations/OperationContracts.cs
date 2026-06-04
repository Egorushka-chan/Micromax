namespace MicroMax.Server.Api.Operations;

public sealed record ReceiveRequest(
    int ProductId,
    int TargetCellId,
    decimal Quantity,
    string? Comment);

public sealed record MoveRequest(
    int ProductId,
    int SourceCellId,
    int TargetCellId,
    decimal Quantity,
    string? Comment);

public sealed record WriteOffRequest(
    int ProductId,
    int SourceCellId,
    decimal Quantity,
    string? Comment);

public sealed record AdjustRequest(
    int ProductId,
    int TargetCellId,
    decimal TargetQuantity,
    string? Comment);

public sealed record WarehouseOperationResponse(
    int Id,
    int WarehouseId,
    string Type,
    string ProductName,
    string? SourceCell,
    string? TargetCell,
    int? AppUserId,
    string? PerformedBy,
    decimal Quantity,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record WarehouseOperationResultResponse(
    int Id,
    string Type,
    decimal Quantity,
    DateTimeOffset CreatedAt);
