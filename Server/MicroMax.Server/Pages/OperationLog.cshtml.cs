using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class OperationLogModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<AdminWarehouseOption> Warehouses { get; private set; } = [];
    public List<UserOption> Users { get; private set; } = [];
    public List<ProductOption> Products { get; private set; } = [];
    public List<CellOption> Cells { get; private set; } = [];
    public List<OperationLogRow> Rows { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CellId { get; set; }

    [BindProperty(SupportsGet = true)]
    public WarehouseOperationType? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);

        Warehouses = await GetAdminWarehouseOptionsAsync(cancellationToken);
        if (WarehouseId is not null && !adminWarehouseIds.Contains(WarehouseId.Value))
        {
            WarehouseId = null;
        }

        Users = await Db.AppUsers
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserOption(x.Id, x.DisplayName))
            .ToListAsync(cancellationToken);

        Products = await Db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductOption(x.Id, $"{x.Name} ({x.Sku})"))
            .ToListAsync(cancellationToken);

        Cells = await Db.StorageCells
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.StorageZone!.WarehouseId) && (WarehouseId == null || x.StorageZone.WarehouseId == WarehouseId.Value))
            .OrderBy(x => x.StorageZone!.Code)
            .ThenBy(x => x.Code)
            .Select(x => new CellOption(x.Id, $"{x.StorageZone!.Warehouse!.Name} / {x.StorageZone.Code} / {x.Code}"))
            .ToListAsync(cancellationToken);

        if (CellId is not null && Cells.All(x => x.Id != CellId.Value))
        {
            CellId = null;
        }

        var operationQuery = Db.WarehouseOperations
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.WarehouseId));

        if (WarehouseId is not null)
        {
            operationQuery = operationQuery.Where(x => x.WarehouseId == WarehouseId.Value);
        }

        if (UserId is not null)
        {
            operationQuery = operationQuery.Where(x => x.AppUserId == UserId.Value);
        }

        if (ProductId is not null)
        {
            operationQuery = operationQuery.Where(x => x.ProductId == ProductId.Value);
        }

        if (CellId is not null)
        {
            operationQuery = operationQuery.Where(x => x.SourceCellId == CellId.Value || x.TargetCellId == CellId.Value);
        }

        if (Type is not null)
        {
            operationQuery = operationQuery.Where(x => x.Type == Type.Value);
        }

        if (FromDate is not null)
        {
            var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(FromDate.Value.Date, DateTimeKind.Utc));
            operationQuery = operationQuery.Where(x => x.CreatedAt >= fromUtc);
        }

        if (ToDate is not null)
        {
            var toExclusiveUtc = new DateTimeOffset(DateTime.SpecifyKind(ToDate.Value.Date.AddDays(1), DateTimeKind.Utc));
            operationQuery = operationQuery.Where(x => x.CreatedAt < toExclusiveUtc);
        }

        Rows = await operationQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new OperationLogRow(
                x.Id,
                x.CreatedAt,
                x.Warehouse!.Name,
                x.Type,
                x.Product!.Name,
                x.Quantity,
                x.AppUser == null ? "Системная операция" : x.AppUser.DisplayName,
                x.SourceCell == null ? null : x.SourceCell.Code,
                x.TargetCell == null ? null : x.TargetCell.Code,
                x.Comment,
                x.Logs.OrderByDescending(y => y.CreatedAt).Select(y => y.Message).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public sealed record UserOption(int Id, string DisplayName);
    public sealed record ProductOption(int Id, string DisplayName);
    public sealed record CellOption(int Id, string DisplayName);

    public sealed record OperationLogRow(
        int Id,
        DateTimeOffset CreatedAt,
        string WarehouseName,
        WarehouseOperationType Type,
        string ProductName,
        decimal Quantity,
        string PerformedBy,
        string? SourceCellCode,
        string? TargetCellCode,
        string? Comment,
        string? LogMessage);
}
