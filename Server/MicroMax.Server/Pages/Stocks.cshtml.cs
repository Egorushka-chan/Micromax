using MicroMax.Server.Data;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class StocksModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<AdminWarehouseOption> Warehouses { get; private set; } = [];
    public List<ZoneOption> Zones { get; private set; } = [];
    public List<CellOption> Cells { get; private set; } = [];
    public List<ProductOption> Products { get; private set; } = [];
    public List<StockRow> Rows { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ZoneId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CellId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        var normalizedSearch = Search?.Trim();
        var normalizedSearchLower = normalizedSearch?.ToLowerInvariant();

        Warehouses = await GetAdminWarehouseOptionsAsync(cancellationToken);
        if (WarehouseId is not null && !adminWarehouseIds.Contains(WarehouseId.Value))
        {
            WarehouseId = null;
        }

        Zones = await Db.StorageZones
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.WarehouseId) && (WarehouseId == null || x.WarehouseId == WarehouseId.Value))
            .OrderBy(x => x.Code)
            .Select(x => new ZoneOption(x.Id, x.WarehouseId, $"{x.Warehouse!.Name} / {x.Code}"))
            .ToListAsync(cancellationToken);

        if (ZoneId is not null && Zones.All(x => x.Id != ZoneId.Value))
        {
            ZoneId = null;
        }

        Cells = await Db.StorageCells
            .AsNoTracking()
            .Where(x =>
                adminWarehouseIds.Contains(x.StorageZone!.WarehouseId) &&
                (WarehouseId == null || x.StorageZone.WarehouseId == WarehouseId.Value) &&
                (ZoneId == null || x.StorageZoneId == ZoneId.Value))
            .OrderBy(x => x.StorageZone!.Code)
            .ThenBy(x => x.Code)
            .Select(x => new CellOption(x.Id, $"{x.StorageZone!.Warehouse!.Name} / {x.StorageZone.Code} / {x.Code}"))
            .ToListAsync(cancellationToken);

        if (CellId is not null && Cells.All(x => x.Id != CellId.Value))
        {
            CellId = null;
        }

        Products = await Db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductOption(x.Id, $"{x.Name} ({x.Sku})"))
            .ToListAsync(cancellationToken);

        if (ProductId is not null && Products.All(x => x.Id != ProductId.Value))
        {
            ProductId = null;
        }

        var stockQuery = Db.StockBalances
            .AsNoTracking()
            .Where(x => x.Quantity > 0 && adminWarehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId));

        if (WarehouseId is not null)
        {
            stockQuery = stockQuery.Where(x => x.StorageCell!.StorageZone!.WarehouseId == WarehouseId.Value);
        }

        if (ZoneId is not null)
        {
            stockQuery = stockQuery.Where(x => x.StorageCell!.StorageZoneId == ZoneId.Value);
        }

        if (CellId is not null)
        {
            stockQuery = stockQuery.Where(x => x.StorageCellId == CellId.Value);
        }

        if (ProductId is not null)
        {
            stockQuery = stockQuery.Where(x => x.ProductId == ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearchLower))
        {
            stockQuery = stockQuery.Where(x =>
                x.Product!.Name.ToLower().Contains(normalizedSearchLower) ||
                x.Product.Sku.ToLower().Contains(normalizedSearchLower) ||
                x.StorageCell!.Code.ToLower().Contains(normalizedSearchLower) ||
                x.StorageCell.StorageZone!.Code.ToLower().Contains(normalizedSearchLower));
        }

        var rawRows = await stockQuery
            .OrderBy(x => x.Product!.Name)
            .ThenBy(x => x.StorageCell!.StorageZone!.Code)
            .ThenBy(x => x.StorageCell!.Code)
            .Select(x => new RawStockRow(
                x.ProductId,
                x.StorageCellId,
                x.StorageCell!.StorageZone!.WarehouseId,
                x.StorageCell.StorageZone.Warehouse!.Name,
                x.StorageCell.StorageZone.Code,
                x.StorageCell.Code,
                x.Product!.Name,
                x.Product.Sku,
                x.Product.Unit,
                x.Product.MinQuantity,
                x.Quantity))
            .ToListAsync(cancellationToken);

        var productIds = rawRows.Select(x => x.ProductId).Distinct().ToList();
        var totalsByProduct = productIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await Db.StockBalances
                .AsNoTracking()
                .Where(x => x.Quantity > 0 && productIds.Contains(x.ProductId) && adminWarehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId))
                .GroupBy(x => x.ProductId)
                .Select(x => new { x.Key, TotalQuantity = x.Sum(y => y.Quantity) })
                .ToDictionaryAsync(x => x.Key, x => x.TotalQuantity, cancellationToken);

        Rows = rawRows
            .Select(x =>
            {
                totalsByProduct.TryGetValue(x.ProductId, out var totalQuantity);
                return new StockRow(
                    x.ProductId,
                    x.CellId,
                    x.WarehouseId,
                    x.WarehouseName,
                    x.ZoneCode,
                    x.CellCode,
                    x.ProductName,
                    x.Sku,
                    x.Unit,
                    x.Quantity,
                    totalQuantity,
                    x.MinQuantity,
                    x.MinQuantity > 0 && totalQuantity <= x.MinQuantity);
            })
            .ToList();
    }

    public sealed record ZoneOption(int Id, int WarehouseId, string DisplayName);
    public sealed record CellOption(int Id, string DisplayName);
    public sealed record ProductOption(int Id, string DisplayName);

    private sealed record RawStockRow(
        int ProductId,
        int CellId,
        int WarehouseId,
        string WarehouseName,
        string ZoneCode,
        string CellCode,
        string ProductName,
        string Sku,
        string Unit,
        decimal MinQuantity,
        decimal Quantity);

    public sealed record StockRow(
        int ProductId,
        int CellId,
        int WarehouseId,
        string WarehouseName,
        string ZoneCode,
        string CellCode,
        string ProductName,
        string Sku,
        string Unit,
        decimal Quantity,
        decimal TotalProductQuantity,
        decimal MinQuantity,
        bool IsLowStock);
}
