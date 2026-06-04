namespace MicroMax.Server.Api.Stocks;

public sealed record StockBalanceResponse(
    int ProductId,
    string ProductName,
    string Sku,
    string Unit,
    int CellId,
    string CellCode,
    string ZoneCode,
    decimal Quantity);
