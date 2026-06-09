using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

/// <summary>
/// Защищённый dashboard админ-панели MicroMax, вынесенный с публичного корня на /admin.
/// </summary>
public sealed class AdminModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public DashboardSummary Summary { get; private set; } = new(0, 0, 0, 0, 0);
    public List<DashboardOperationRow> RecentOperations { get; private set; } = [];
    public List<LowStockRow> LowStockProducts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);

        Summary = new DashboardSummary(
            adminWarehouseIds.Count,
            await Db.StorageZones.CountAsync(x => adminWarehouseIds.Contains(x.WarehouseId), cancellationToken),
            await Db.StorageCells.CountAsync(x => adminWarehouseIds.Contains(x.StorageZone!.WarehouseId), cancellationToken),
            await Db.Products.CountAsync(cancellationToken),
            await Db.StockBalances.CountAsync(
                x => x.Quantity > 0 && adminWarehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId),
                cancellationToken));

        RecentOperations = await Db.WarehouseOperations
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.WarehouseId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new DashboardOperationRow(
                x.Id,
                x.CreatedAt,
                x.Type,
                x.Warehouse!.Name,
                x.Product!.Name,
                x.SourceCell == null ? null : x.SourceCell.Code,
                x.TargetCell == null ? null : x.TargetCell.Code,
                x.Quantity,
                x.AppUser == null ? "Системная операция" : x.AppUser.DisplayName,
                x.Comment))
            .ToListAsync(cancellationToken);

        var lowStockProducts = await Db.Products
            .AsNoTracking()
            .Where(x => x.MinQuantity > 0)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Sku,
                x.Unit,
                x.MinQuantity,
                TotalQuantity = Db.StockBalances
                    .Where(y => y.ProductId == x.Id && adminWarehouseIds.Contains(y.StorageCell!.StorageZone!.WarehouseId))
                    .Sum(y => (decimal?)y.Quantity) ?? 0m
            })
            .Where(x => x.TotalQuantity <= x.MinQuantity)
            .OrderBy(x => x.TotalQuantity)
            .ThenBy(x => x.Name)
            .Take(10)
            .ToListAsync(cancellationToken);

        LowStockProducts = lowStockProducts
            .Select(x => new LowStockRow(
                x.Id,
                x.Name,
                x.Sku,
                x.Unit,
                x.MinQuantity,
                x.TotalQuantity))
            .ToList();
    }

    public sealed record DashboardSummary(
        int WarehouseCount,
        int ZoneCount,
        int CellCount,
        int ProductCount,
        int StockPositionCount);

    public sealed record DashboardOperationRow(
        int Id,
        DateTimeOffset CreatedAt,
        WarehouseOperationType Type,
        string WarehouseName,
        string ProductName,
        string? SourceCellCode,
        string? TargetCellCode,
        decimal Quantity,
        string PerformedBy,
        string? Comment);

    public sealed record LowStockRow(
        int ProductId,
        string Name,
        string Sku,
        string Unit,
        decimal MinQuantity,
        decimal TotalQuantity);
}
