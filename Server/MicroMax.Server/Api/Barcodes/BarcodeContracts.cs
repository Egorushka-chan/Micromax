namespace MicroMax.Server.Api.Barcodes;

public sealed record BarcodeDraftRequest(
    string Value,
    string? Symbology,
    bool? IsPrimary = null);

public sealed record BarcodeResponse(
    int Id,
    string Value,
    string Symbology,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int CreatedByUserId);

public sealed record BarcodeResolveResponse(
    bool Found,
    string Value,
    string? EntityType = null,
    int? EntityId = null,
    string? Title = null,
    string? Subtitle = null);
