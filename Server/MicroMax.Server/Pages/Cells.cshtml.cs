using MicroMax.Server.Data;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class CellsModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<AdminWarehouseOption> Warehouses { get; private set; } = [];
    public List<ZoneOption> Zones { get; private set; } = [];
    public List<CellRow> Cells { get; private set; } = [];
    public CellRow? SelectedCell { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ZoneId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public CellCreateInput CreateForm { get; set; } = new();

    [BindProperty]
    public CellEditInput EditForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        if (CreateForm.ZoneId <= 0)
        {
            SetErrorMessage("Выберите зону хранения.");
            return RedirectToPage(new { warehouseId = WarehouseId, zoneId = ZoneId });
        }

        var zone = await Db.StorageZones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == CreateForm.ZoneId && adminWarehouseIds.Contains(x.WarehouseId), cancellationToken);

        if (zone is null)
        {
            SetErrorMessage("Выбранная зона недоступна для администрирования.");
            return RedirectToPage(new { warehouseId = WarehouseId, zoneId = ZoneId });
        }

        var code = CreateForm.Code.Trim();
        var name = CreateForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите код и название ячейки.");
            return RedirectToPage(new { warehouseId = zone.WarehouseId, zoneId = zone.Id });
        }

        Db.StorageCells.Add(new Models.StorageCell
        {
            StorageZoneId = zone.Id,
            Code = code,
            Name = name
        });

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Ячейка хранения добавлена.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { warehouseId = zone.WarehouseId, zoneId = zone.Id });
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
    {
        if (EditForm.Id <= 0)
        {
            SetErrorMessage("Не удалось определить ячейку для редактирования.");
            return RedirectToPage(new { warehouseId = WarehouseId, zoneId = ZoneId });
        }

        var code = EditForm.Code.Trim();
        var name = EditForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите код и название ячейки.");
            return RedirectToPage(new { warehouseId = WarehouseId, zoneId = ZoneId, editId = EditForm.Id });
        }

        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        var cell = await Db.StorageCells
            .Include(x => x.StorageZone)
            .FirstOrDefaultAsync(
                x => x.Id == EditForm.Id && adminWarehouseIds.Contains(x.StorageZone!.WarehouseId),
                cancellationToken);

        if (cell is null)
        {
            SetErrorMessage("Ячейка не найдена или недоступна для редактирования.");
            return RedirectToPage(new { warehouseId = WarehouseId, zoneId = ZoneId });
        }

        cell.Code = code;
        cell.Name = name;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Данные ячейки обновлены.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { warehouseId = WarehouseId ?? cell.StorageZone!.WarehouseId, zoneId = ZoneId ?? cell.StorageZoneId, editId = cell.Id });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
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

        var query = Db.StorageCells
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.StorageZone!.WarehouseId));

        if (WarehouseId is not null)
        {
            query = query.Where(x => x.StorageZone!.WarehouseId == WarehouseId.Value);
        }

        if (ZoneId is not null)
        {
            query = query.Where(x => x.StorageZoneId == ZoneId.Value);
        }

        Cells = await query
            .OrderBy(x => x.StorageZone!.Warehouse!.Name)
            .ThenBy(x => x.StorageZone!.Code)
            .ThenBy(x => x.Code)
            .Select(x => new CellRow(
                x.Id,
                x.StorageZoneId,
                x.StorageZone!.WarehouseId,
                x.StorageZone.Warehouse!.Name,
                x.StorageZone.Code,
                x.Code,
                x.Name,
                x.Balances.Count(y => y.Quantity > 0)))
            .ToListAsync(cancellationToken);

        if (EditId is null)
        {
            return;
        }

        SelectedCell = Cells.FirstOrDefault(x => x.Id == EditId.Value);
        if (SelectedCell is null)
        {
            return;
        }

        EditForm = new CellEditInput
        {
            Id = SelectedCell.Id,
            Code = SelectedCell.Code,
            Name = SelectedCell.Name
        };
    }

    public sealed class CellCreateInput
    {
        public int ZoneId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CellEditInput
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed record ZoneOption(int Id, int WarehouseId, string DisplayName);

    public sealed record CellRow(
        int Id,
        int ZoneId,
        int WarehouseId,
        string WarehouseName,
        string ZoneCode,
        string Code,
        string Name,
        int ActiveStockCount);
}
