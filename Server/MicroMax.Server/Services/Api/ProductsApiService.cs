using MicroMax.Server.Api.Barcodes;
using MicroMax.Server.Api.Products;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class ProductsApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService,
    BarcodesApiService barcodesApiService)
{
    public async Task<IReadOnlyList<ProductResponse>> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureAnyWarehouseAccessAsync(userId, cancellationToken);

        return await db.Products
            .OrderBy(x => x.Name)
            .Select(x => new ProductResponse(x.Id, x.Sku, x.Name, x.Unit, x.MinQuantity))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductLocationResponse>> GetLocationsAsync(
        int userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Products.AnyAsync(x => x.Id == productId, cancellationToken))
        {
            throw new ApiNotFoundException("Номенклатура не найдена.");
        }

        var warehouseIds = await warehousePermissionService.GetAccessibleWarehouseIdsAsync(userId, cancellationToken);

        return await db.StockBalances
            .Where(x => x.ProductId == productId && x.Quantity > 0 && warehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId))
            .Select(x => new ProductLocationResponse(
                x.StorageCellId,
                x.StorageCell!.Code,
                x.StorageCell.StorageZone!.Code,
                x.Quantity))
            .ToListAsync(cancellationToken);
    }

    public Task<ProductResponse> CreateAsync(
        int userId,
        CreateProductRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAsync(userId, null, request, cancellationToken);

    public Task<ProductResponse> CreateAsync(
        int userId,
        int warehouseId,
        CreateProductRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAsync(userId, (int?)warehouseId, request, cancellationToken);

    public async Task<ProductResponse> CreateAsync(
        int userId,
        int? warehouseId,
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, warehouseId.Value, cancellationToken);
        }

        var sku = request.Sku.Trim();
        if (await db.Products.AnyAsync(x => x.Sku == sku, cancellationToken))
        {
            throw new ApiConflictException("Номенклатура с таким SKU уже существует.");
        }

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            MinQuantity = request.MinQuantity
        };

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        if (request.InitialBarcode is not null)
        {
            if (warehouseId is null)
            {
                await barcodesApiService.CreateProductBarcodeAsync(
                    userId,
                    product.Id,
                    request.InitialBarcode,
                    cancellationToken);
            }
            else
            {
                await barcodesApiService.CreateProductBarcodeAsync(
                    userId,
                    warehouseId.Value,
                    product.Id,
                    request.InitialBarcode,
                    cancellationToken);
            }
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return ToResponse(product);
    }

    public Task<ProductResponse> UpdateAsync(
        int userId,
        int productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(userId, null, productId, request, cancellationToken);

    public Task<ProductResponse> UpdateAsync(
        int userId,
        int warehouseId,
        int productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(userId, (int?)warehouseId, productId, request, cancellationToken);

    public async Task<ProductResponse> UpdateAsync(
        int userId,
        int? warehouseId,
        int productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureProductManagementAccessAsync(userId, warehouseId.Value, cancellationToken);
        }

        var product = await db.Products.FindAsync([productId], cancellationToken)
            ?? throw new ApiNotFoundException("Номенклатура не найдена.");

        var sku = request.Sku.Trim();
        if (await db.Products.AnyAsync(x => x.Id != productId && x.Sku == sku, cancellationToken))
        {
            throw new ApiConflictException("Номенклатура с таким SKU уже существует.");
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Unit = request.Unit.Trim();
        product.MinQuantity = request.MinQuantity;
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(product);
    }

    public async Task DeleteAsync(int userId, int productId, CancellationToken cancellationToken = default)
    {
        await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);

        var product = await db.Products.FindAsync([productId], cancellationToken)
            ?? throw new ApiNotFoundException("Номенклатура не найдена.");

        var activeBarcodes = await db.Barcodes
            .Where(x => x.IsActive && x.EntityType == BarcodeEntityType.Product && x.EntityId == productId)
            .ToListAsync(cancellationToken);

        foreach (var barcode in activeBarcodes)
        {
            barcode.IsActive = false;
            barcode.IsPrimary = false;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ProductResponse ToResponse(Product product) =>
        new(product.Id, product.Sku, product.Name, product.Unit, product.MinQuantity);
}
