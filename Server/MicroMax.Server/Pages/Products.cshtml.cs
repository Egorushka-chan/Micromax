using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Pages;

public sealed class ProductsModel(
    MicroMaxDbContext db,
    CurrentUserService currentUserService) : AdminPageModel(db, currentUserService)
{
    public List<ProductRow> Products { get; private set; } = [];
    public ProductRow? SelectedProduct { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty]
    public ProductInput CreateForm { get; set; } = new();

    [BindProperty]
    public ProductEditInput EditForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var sku = CreateForm.Sku.Trim();
        var name = CreateForm.Name.Trim();
        var unit = CreateForm.Unit.Trim();

        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
        {
            SetErrorMessage("Укажите артикул, название и единицу измерения.");
            return RedirectToPage(new { search = Search });
        }

        Db.Products.Add(new Product
        {
            Sku = sku,
            Name = name,
            Unit = unit,
            MinQuantity = CreateForm.MinQuantity
        });

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Номенклатура добавлена.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { search = Search });
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken cancellationToken)
    {
        if (EditForm.Id <= 0)
        {
            SetErrorMessage("Не удалось определить позицию номенклатуры.");
            return RedirectToPage(new { search = Search });
        }

        var sku = EditForm.Sku.Trim();
        var name = EditForm.Name.Trim();
        var unit = EditForm.Unit.Trim();

        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
        {
            SetErrorMessage("Укажите артикул, название и единицу измерения.");
            return RedirectToPage(new { search = Search, editId = EditForm.Id });
        }

        var product = await Db.Products.FirstOrDefaultAsync(x => x.Id == EditForm.Id, cancellationToken);
        if (product is null)
        {
            SetErrorMessage("Номенклатура не найдена.");
            return RedirectToPage(new { search = Search });
        }

        product.Sku = sku;
        product.Name = name;
        product.Unit = unit;
        product.MinQuantity = EditForm.MinQuantity;

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            SetSuccessMessage("Данные номенклатуры обновлены.");
        }
        catch (DbUpdateException ex)
        {
            SetErrorMessage(ex);
        }

        return RedirectToPage(new { search = Search, editId = EditForm.Id });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var adminWarehouseIds = await GetAdminWarehouseIdsAsync(cancellationToken);
        var normalizedSearch = Search?.Trim();
        var normalizedSearchLower = normalizedSearch?.ToLowerInvariant();

        var query = Db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(normalizedSearchLower))
        {
            query = query.Where(x =>
                x.Name.ToLower().Contains(normalizedSearchLower) ||
                x.Sku.ToLower().Contains(normalizedSearchLower));
        }

        Products = await query
            .OrderBy(x => x.Name)
            .Select(x => new ProductRow(
                x.Id,
                x.Sku,
                x.Name,
                x.Unit,
                x.MinQuantity,
                Db.StockBalances.Count(y => y.ProductId == x.Id && y.Quantity > 0 && adminWarehouseIds.Contains(y.StorageCell!.StorageZone!.WarehouseId))))
            .ToListAsync(cancellationToken);

        if (EditId is null)
        {
            return;
        }

        SelectedProduct = Products.FirstOrDefault(x => x.Id == EditId.Value);
        if (SelectedProduct is null)
        {
            return;
        }

        EditForm = new ProductEditInput
        {
            Id = SelectedProduct.Id,
            Sku = SelectedProduct.Sku,
            Name = SelectedProduct.Name,
            Unit = SelectedProduct.Unit,
            MinQuantity = SelectedProduct.MinQuantity
        };
    }

    public class ProductInput
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "шт";
        public decimal MinQuantity { get; set; }
    }

    public sealed class ProductEditInput : ProductInput
    {
        public int Id { get; set; }
    }

    public sealed record ProductRow(
        int Id,
        string Sku,
        string Name,
        string Unit,
        decimal MinQuantity,
        int ActiveLocationCount);
}
