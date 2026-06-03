using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class IndexModel(MicroMaxDbContext db, WarehouseOperationService operations) : PageModel
{
    public List<Warehouse> Warehouses { get; private set; } = [];
    public List<StorageZone> Zones { get; private set; } = [];
    public List<StorageCell> Cells { get; private set; } = [];
    public List<Product> Products { get; private set; } = [];
    public List<UserRole> Roles { get; private set; } = [];
    public List<AppUser> Users { get; private set; } = [];
    public List<StockRow> StockRows { get; private set; } = [];
    public List<OperationRow> OperationRows { get; private set; } = [];

    [BindProperty] public WarehouseInput WarehouseForm { get; set; } = new();
    [BindProperty] public ZoneInput ZoneForm { get; set; } = new();
    [BindProperty] public CellInput CellForm { get; set; } = new();
    [BindProperty] public ProductInput ProductForm { get; set; } = new();
    [BindProperty] public RoleInput RoleForm { get; set; } = new();
    [BindProperty] public UserInput UserForm { get; set; } = new();
    [BindProperty] public OperationInput OperationForm { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddWarehouseAsync()
    {
        db.Warehouses.Add(new Warehouse { Name = WarehouseForm.Name, Address = WarehouseForm.Address });
        return await SaveAndRedirectAsync("Склад добавлен.");
    }

    public async Task<IActionResult> OnPostAddZoneAsync()
    {
        db.StorageZones.Add(new StorageZone { WarehouseId = ZoneForm.WarehouseId, Code = ZoneForm.Code, Name = ZoneForm.Name });
        return await SaveAndRedirectAsync("Зона хранения добавлена.");
    }

    public async Task<IActionResult> OnPostAddCellAsync()
    {
        db.StorageCells.Add(new StorageCell { StorageZoneId = CellForm.StorageZoneId, Code = CellForm.Code, Name = CellForm.Name });
        return await SaveAndRedirectAsync("Ячейка хранения добавлена.");
    }

    public async Task<IActionResult> OnPostAddProductAsync()
    {
        db.Products.Add(new Product { Sku = ProductForm.Sku, Name = ProductForm.Name, Unit = ProductForm.Unit, MinQuantity = ProductForm.MinQuantity });
        return await SaveAndRedirectAsync("Номенклатура добавлена.");
    }

    public async Task<IActionResult> OnPostAddRoleAsync()
    {
        db.UserRoles.Add(new UserRole { Name = RoleForm.Name });
        return await SaveAndRedirectAsync("Роль добавлена.");
    }

    public async Task<IActionResult> OnPostAddUserAsync()
    {
        db.AppUsers.Add(new AppUser { Login = UserForm.Login, DisplayName = UserForm.DisplayName, UserRoleId = UserForm.UserRoleId });
        return await SaveAndRedirectAsync("Пользователь добавлен.");
    }

    public async Task<IActionResult> OnPostRunOperationAsync()
    {
        try
        {
            _ = OperationForm.Type switch
            {
                WarehouseOperationType.Receive => await operations.ReceiveAsync(new ReceiveRequest(OperationForm.ProductId, OperationForm.TargetCellId!.Value, OperationForm.Quantity, OperationForm.UserId, OperationForm.Comment)),
                WarehouseOperationType.Move => await operations.MoveAsync(new MoveRequest(OperationForm.ProductId, OperationForm.SourceCellId!.Value, OperationForm.TargetCellId!.Value, OperationForm.Quantity, OperationForm.UserId, OperationForm.Comment)),
                WarehouseOperationType.WriteOff => await operations.WriteOffAsync(new WriteOffRequest(OperationForm.ProductId, OperationForm.SourceCellId!.Value, OperationForm.Quantity, OperationForm.UserId, OperationForm.Comment)),
                WarehouseOperationType.Adjust => await operations.AdjustAsync(new AdjustRequest(OperationForm.ProductId, OperationForm.TargetCellId!.Value, OperationForm.Quantity, OperationForm.UserId, OperationForm.Comment)),
                _ => throw new InvalidOperationException("Неизвестный тип операции.")
            };

            TempData["Message"] = "Складская операция выполнена.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage();
    }

    private async Task<IActionResult> SaveAndRedirectAsync(string message)
    {
        try
        {
            await db.SaveChangesAsync();
            TempData["Message"] = message;
        }
        catch (DbUpdateException ex)
        {
            TempData["Error"] = ex.InnerException?.Message ?? ex.Message;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Warehouses = await db.Warehouses.OrderBy(x => x.Name).ToListAsync();
        Zones = await db.StorageZones.Include(x => x.Warehouse).OrderBy(x => x.Code).ToListAsync();
        Cells = await db.StorageCells.Include(x => x.StorageZone).ThenInclude(x => x!.Warehouse).OrderBy(x => x.Code).ToListAsync();
        Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
        Roles = await db.UserRoles.OrderBy(x => x.Name).ToListAsync();
        Users = await db.AppUsers.Include(x => x.UserRole).OrderBy(x => x.DisplayName).ToListAsync();
        StockRows = await db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.Quantity > 0)
            .OrderBy(x => x.Product!.Name)
            .ThenBy(x => x.StorageCell!.Code)
            .Select(x => new StockRow(x.Product!.Name, x.Product.Sku, x.StorageCell!.Code, x.StorageCell.StorageZone!.Code, x.Quantity, x.Product.Unit))
            .ToListAsync();
        OperationRows = await db.WarehouseOperations
            .Include(x => x.Product)
            .Include(x => x.SourceCell)
            .Include(x => x.TargetCell)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new OperationRow(x.Id, x.Type, x.Product!.Name, x.SourceCell == null ? "" : x.SourceCell.Code, x.TargetCell == null ? "" : x.TargetCell.Code, x.Quantity, x.CreatedAt, x.Comment ?? ""))
            .ToListAsync();
    }

    public sealed class WarehouseInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
    }

    public sealed class ZoneInput
    {
        public int WarehouseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CellInput
    {
        public int StorageZoneId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ProductInput
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "шт";
        public decimal MinQuantity { get; set; }
    }

    public sealed class RoleInput
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UserInput
    {
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int UserRoleId { get; set; }
    }

    public sealed class OperationInput
    {
        public WarehouseOperationType Type { get; set; } = WarehouseOperationType.Receive;
        public int ProductId { get; set; }
        public int? SourceCellId { get; set; }
        public int? TargetCellId { get; set; }
        public int? UserId { get; set; }
        public decimal Quantity { get; set; }
        public string? Comment { get; set; }
    }

    public sealed record StockRow(string ProductName, string Sku, string CellCode, string ZoneCode, decimal Quantity, string Unit);
    public sealed record OperationRow(int Id, WarehouseOperationType Type, string ProductName, string SourceCell, string TargetCell, decimal Quantity, DateTimeOffset CreatedAt, string Comment);
}
