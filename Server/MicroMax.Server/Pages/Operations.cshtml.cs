using MicroMax.Server.Api.Operations;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class OperationsModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService,
    WarehouseOperationService warehouseOperationService,
    WarehousePermissionService warehousePermissionService) : AdminPageModel(db, currentUserService)
{
    public List<ProductOption> ProductOptions { get; private set; } = [];
    public List<CellOption> CellOptions { get; private set; } = [];
    public List<OperationRow> RecentOperations { get; private set; } = [];

    public string PerformerName => User.Identity?.Name ?? "Администратор";

    [BindProperty]
    public OperationInput OperationForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostRunAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (OperationForm.ProductId <= 0)
        {
            SetErrorMessage("Выберите номенклатуру.");
            return RedirectToPage();
        }

        var requiresPositiveQuantity = OperationForm.Type != WarehouseOperationType.Adjust;
        if ((requiresPositiveQuantity && OperationForm.Quantity <= 0) || (!requiresPositiveQuantity && OperationForm.Quantity < 0))
        {
            SetErrorMessage(OperationForm.Type == WarehouseOperationType.Adjust
                ? "Итоговый остаток не может быть отрицательным."
                : "Количество должно быть положительным.");
            return RedirectToPage();
        }

        try
        {
            switch (OperationForm.Type)
            {
                case WarehouseOperationType.Receive:
                    if (OperationForm.TargetCellId is null)
                    {
                        throw new InvalidOperationException("Для приёмки необходимо указать целевую ячейку.");
                    }

                    await warehousePermissionService.EnsureOperationAccessAsync(userId, null, OperationForm.TargetCellId, cancellationToken);
                    await warehouseOperationService.ReceiveAsync(
                        new ReceiveRequest(
                            OperationForm.ProductId,
                            OperationForm.TargetCellId.Value,
                            OperationForm.Quantity,
                            NormalizeOptionalText(OperationForm.Comment)),
                        userId,
                        cancellationToken);
                    break;

                case WarehouseOperationType.Move:
                    if (OperationForm.SourceCellId is null || OperationForm.TargetCellId is null)
                    {
                        throw new InvalidOperationException("Для перемещения необходимо указать исходную и целевую ячейки.");
                    }

                    if (OperationForm.SourceCellId == OperationForm.TargetCellId)
                    {
                        throw new InvalidOperationException("Исходная и целевая ячейки должны отличаться.");
                    }

                    await warehousePermissionService.EnsureOperationAccessAsync(
                        userId,
                        OperationForm.SourceCellId,
                        OperationForm.TargetCellId,
                        cancellationToken);
                    await warehouseOperationService.MoveAsync(
                        new MoveRequest(
                            OperationForm.ProductId,
                            OperationForm.SourceCellId.Value,
                            OperationForm.TargetCellId.Value,
                            OperationForm.Quantity,
                            NormalizeOptionalText(OperationForm.Comment)),
                        userId,
                        cancellationToken);
                    break;

                case WarehouseOperationType.WriteOff:
                    if (OperationForm.SourceCellId is null)
                    {
                        throw new InvalidOperationException("Для списания необходимо указать исходную ячейку.");
                    }

                    await warehousePermissionService.EnsureOperationAccessAsync(userId, OperationForm.SourceCellId, null, cancellationToken);
                    await warehouseOperationService.WriteOffAsync(
                        new WriteOffRequest(
                            OperationForm.ProductId,
                            OperationForm.SourceCellId.Value,
                            OperationForm.Quantity,
                            NormalizeOptionalText(OperationForm.Comment)),
                        userId,
                        cancellationToken);
                    break;

                case WarehouseOperationType.Adjust:
                    if (OperationForm.TargetCellId is null)
                    {
                        throw new InvalidOperationException("Для корректировки необходимо указать целевую ячейку.");
                    }

                    await warehousePermissionService.EnsureOperationAccessAsync(userId, null, OperationForm.TargetCellId, cancellationToken);
                    await warehouseOperationService.AdjustAsync(
                        new AdjustRequest(
                            OperationForm.ProductId,
                            OperationForm.TargetCellId.Value,
                            OperationForm.Quantity,
                            NormalizeOptionalText(OperationForm.Comment)),
                        userId,
                        cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException("Выбран неподдерживаемый тип складской операции.");
            }

            SetSuccessMessage("Складская операция выполнена.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);

        ProductOptions = await Db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductOption(x.Id, $"{x.Name} ({x.Sku})"))
            .ToListAsync(cancellationToken);

        CellOptions = await Db.StorageCells
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.StorageZone!.WarehouseId))
            .OrderBy(x => x.StorageZone!.Warehouse!.Name)
            .ThenBy(x => x.StorageZone!.Code)
            .ThenBy(x => x.Code)
            .Select(x => new CellOption(x.Id, $"{x.StorageZone!.Warehouse!.Name} / {x.StorageZone.Code} / {x.Code}"))
            .ToListAsync(cancellationToken);

        RecentOperations = await Db.WarehouseOperations
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.WarehouseId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new OperationRow(
                x.Id,
                x.CreatedAt,
                x.Type,
                x.Product!.Name,
                x.SourceCell == null ? null : x.SourceCell.Code,
                x.TargetCell == null ? null : x.TargetCell.Code,
                x.Quantity,
                x.AppUser == null ? "Системная операция" : x.AppUser.DisplayName,
                x.Comment))
            .ToListAsync(cancellationToken);
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class OperationInput
    {
        public WarehouseOperationType Type { get; set; } = WarehouseOperationType.Receive;
        public int ProductId { get; set; }
        public int? SourceCellId { get; set; }
        public int? TargetCellId { get; set; }
        public decimal Quantity { get; set; }
        public string? Comment { get; set; }
    }

    public sealed record ProductOption(int Id, string DisplayName);
    public sealed record CellOption(int Id, string DisplayName);

    public sealed record OperationRow(
        int Id,
        DateTimeOffset CreatedAt,
        WarehouseOperationType Type,
        string ProductName,
        string? SourceCellCode,
        string? TargetCellCode,
        decimal Quantity,
        string PerformedBy,
        string? Comment);
}
