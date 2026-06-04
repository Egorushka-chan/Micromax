using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class ZonesModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<AdminWarehouseOption> Warehouses { get; private set; } = [];
    public List<ZoneRow> Zones { get; private set; } = [];
    public ZoneRow? SelectedZone { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int? WarehouseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public ZoneCreateInput CreateForm { get; set; } = new();

    [BindProperty]
    public ZoneEditInput EditForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        if (CreateForm.WarehouseId <= 0 || !adminWarehouseIds.Contains(CreateForm.WarehouseId))
        {
            SetErrorMessage("Выберите склад, доступный для администрирования.");
            return RedirectToPage(new { warehouseId = WarehouseId });
        }

        var code = CreateForm.Code.Trim();
        var name = CreateForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите код и название зоны хранения.");
            return RedirectToPage(new { warehouseId = CreateForm.WarehouseId });
        }

        Db.StorageZones.Add(new StorageZone
        {
            WarehouseId = CreateForm.WarehouseId,
            Code = code,
            Name = name
        });

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Зона хранения добавлена.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { warehouseId = CreateForm.WarehouseId });
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
    {
        if (EditForm.Id <= 0)
        {
            SetErrorMessage("Не удалось определить зону для редактирования.");
            return RedirectToPage(new { warehouseId = WarehouseId });
        }

        var code = EditForm.Code.Trim();
        var name = EditForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите код и название зоны хранения.");
            return RedirectToPage(new { warehouseId = WarehouseId, editId = EditForm.Id });
        }

        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        var zone = await Db.StorageZones.FirstOrDefaultAsync(
            x => x.Id == EditForm.Id && adminWarehouseIds.Contains(x.WarehouseId),
            cancellationToken);

        if (zone is null)
        {
            SetErrorMessage("Зона не найдена или недоступна для редактирования.");
            return RedirectToPage(new { warehouseId = WarehouseId });
        }

        zone.Code = code;
        zone.Name = name;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Данные зоны обновлены.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { warehouseId = WarehouseId ?? zone.WarehouseId, editId = zone.Id });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        Warehouses = await GetAdminWarehouseOptionsAsync(cancellationToken);

        if (WarehouseId is not null && !adminWarehouseIds.Contains(WarehouseId.Value))
        {
            WarehouseId = null;
        }

        var query = Db.StorageZones
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.WarehouseId));

        if (WarehouseId is not null)
        {
            query = query.Where(x => x.WarehouseId == WarehouseId.Value);
        }

        Zones = await query
            .OrderBy(x => x.Warehouse!.Name)
            .ThenBy(x => x.Code)
            .Select(x => new ZoneRow(
                x.Id,
                x.WarehouseId,
                x.Warehouse!.Name,
                x.Code,
                x.Name,
                x.Cells.Count))
            .ToListAsync(cancellationToken);

        if (EditId is null)
        {
            return;
        }

        SelectedZone = Zones.FirstOrDefault(x => x.Id == EditId.Value);
        if (SelectedZone is null)
        {
            return;
        }

        EditForm = new ZoneEditInput
        {
            Id = SelectedZone.Id,
            Code = SelectedZone.Code,
            Name = SelectedZone.Name
        };
    }

    public sealed class ZoneCreateInput
    {
        public int WarehouseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ZoneEditInput
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed record ZoneRow(
        int Id,
        int WarehouseId,
        string WarehouseName,
        string Code,
        string Name,
        int CellCount);
}
