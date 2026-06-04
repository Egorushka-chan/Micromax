using MicroMax.Server.Api.Barcodes;

namespace MicroMax.Server.Api.Products;

public sealed record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string Unit,
    decimal MinQuantity);

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Unit,
    decimal MinQuantity,
    BarcodeDraftRequest? InitialBarcode = null);

public sealed record UpdateProductRequest(
    string Sku,
    string Name,
    string Unit,
    decimal MinQuantity);

public sealed record ProductLocationResponse(
    int CellId,
    string CellCode,
    string ZoneCode,
    decimal Quantity);
