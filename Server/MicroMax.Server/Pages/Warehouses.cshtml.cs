using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class WarehousesModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<WarehouseRow> Warehouses { get; private set; } = [];
    public WarehouseRow? SelectedWarehouse { get; private set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public WarehouseInput CreateForm { get; set; } = new();

    [BindProperty]
    public WarehouseEditInput EditForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var name = CreateForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите название склада.");
            return RedirectToPage();
        }

        var userId = GetCurrentUserId();
        var adminRoleId = await Db.Roles
            .Where(x => x.Code == SystemRoleCodes.Admin)
            .Select(x => x.Id)
            .FirstAsync(cancellationToken);

        var warehouse = new Warehouse
        {
            Name = name,
            Address = NormalizeOptionalText(CreateForm.Address)
        };

        Db.Warehouses.Add(warehouse);
        Db.WarehouseUsers.Add(new WarehouseUser
        {
            Warehouse = warehouse,
            UserId = userId,
            RoleId = adminRoleId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Склад добавлен.");
            return RedirectToPage(new { editId = warehouse.Id });
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
    {
        if (EditForm.Id <= 0)
        {
            SetErrorMessage("Не удалось определить склад для редактирования.");
            return RedirectToPage();
        }

        var name = EditForm.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetErrorMessage("Укажите название склада.");
            return RedirectToPage(new { editId = EditForm.Id });
        }

        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        var warehouse = await Db.Warehouses.FirstOrDefaultAsync(
            x => x.Id == EditForm.Id && adminWarehouseIds.Contains(x.Id),
            cancellationToken);

        if (warehouse is null)
        {
            SetErrorMessage("Склад не найден или недоступен для редактирования.");
            return RedirectToPage();
        }

        warehouse.Name = name;
        warehouse.Address = NormalizeOptionalText(EditForm.Address);

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Данные склада обновлены.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { editId = EditForm.Id });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);

        Warehouses = await Db.Warehouses
            .AsNoTracking()
            .Where(x => adminWarehouseIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseRow(
                x.Id,
                x.Name,
                x.Address,
                x.Zones.Count,
                x.Zones.SelectMany(y => y.Cells).Count(),
                x.Users.Count))
            .ToListAsync(cancellationToken);

        if (EditId is null)
        {
            return;
        }

        SelectedWarehouse = Warehouses.FirstOrDefault(x => x.Id == EditId.Value);
        if (SelectedWarehouse is null)
        {
            return;
        }

        EditForm = new WarehouseEditInput
        {
            Id = SelectedWarehouse.Id,
            Name = SelectedWarehouse.Name,
            Address = SelectedWarehouse.Address
        };
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public class WarehouseInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
    }

    public sealed class WarehouseEditInput : WarehouseInput
    {
        public int Id { get; set; }
    }

    public sealed record WarehouseRow(
        int Id,
        string Name,
        string? Address,
        int ZoneCount,
        int CellCount,
        int UserCount);
}
