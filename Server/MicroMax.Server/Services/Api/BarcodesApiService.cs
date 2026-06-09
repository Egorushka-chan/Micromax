using MicroMax.Server.Api.Barcodes;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class BarcodesApiService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    public Task<BarcodeResolveResponse> ResolveAsync(
        int userId,
        string value,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(userId, null, value, cancellationToken);

    public async Task<BarcodeResolveResponse> ResolveAsync(
        int userId,
        int? warehouseId,
        string value,
        CancellationToken cancellationToken = default)
    {
        var normalizedValue = NormalizeValue(value);

        if (warehouseId is not null)
        {
            await warehousePermissionService.EnsureWarehouseAccessAsync(userId, warehouseId.Value, cancellationToken);
        }

        var barcode = await db.Barcodes
            .AsNoTracking()
            .Where(x => x.IsActive && x.Value == normalizedValue)
            .Select(x => new BarcodeLookupRow(x.EntityType, x.EntityId))
            .FirstOrDefaultAsync(cancellationToken);

        if (barcode is null)
        {
            return new BarcodeResolveResponse(false, normalizedValue);
        }

        if (warehouseId is not null && barcode.EntityType == BarcodeEntityType.Cell)
        {
            var barcodeWarehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(barcode.EntityId, cancellationToken);
            if (barcodeWarehouseId != warehouseId.Value)
            {
                return new BarcodeResolveResponse(false, normalizedValue);
            }
        }

        return barcode.EntityType switch
        {
            BarcodeEntityType.Product => await ResolveProductAsync(userId, warehouseId, normalizedValue, barcode.EntityId, cancellationToken),
            BarcodeEntityType.Cell => await ResolveCellAsync(userId, normalizedValue, barcode.EntityId, cancellationToken),
            _ => throw new ApiValidationException("Не удалось определить тип объекта, привязанного к штрих-коду.")
        };
    }

    public Task<IReadOnlyList<BarcodeResponse>> GetProductBarcodesAsync(
        int userId,
        int productId,
        CancellationToken cancellationToken = default) =>
        GetProductBarcodesAsync(userId, null, productId, cancellationToken);

    public async Task<IReadOnlyList<BarcodeResponse>> GetProductBarcodesAsync(
        int userId,
        int? warehouseId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureAnyWarehouseAccessAsync(userId, cancellationToken);
        }
        else
        {
            await warehousePermissionService.EnsureWarehousePermissionAsync(
                userId,
                warehouseId.Value,
                WarehousePermission.ProductRead,
                cancellationToken);
        }

        if (!await db.Products.AnyAsync(x => x.Id == productId, cancellationToken))
        {
            throw new ApiNotFoundException("Номенклатура не найдена.");
        }

        return await GetEntityBarcodesAsync(BarcodeEntityType.Product, productId, cancellationToken);
    }

    public async Task<IReadOnlyList<BarcodeResponse>> GetCellBarcodesAsync(
        int userId,
        int cellId,
        CancellationToken cancellationToken = default)
    {
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(cellId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.StockRead,
            cancellationToken);

        return await GetEntityBarcodesAsync(BarcodeEntityType.Cell, cellId, cancellationToken);
    }

    public Task<BarcodeResponse> CreateProductBarcodeAsync(
        int userId,
        int productId,
        BarcodeDraftRequest request,
        CancellationToken cancellationToken = default) =>
        CreateProductBarcodeAsync(userId, null, productId, request, cancellationToken);

    public async Task<BarcodeResponse> CreateProductBarcodeAsync(
        int userId,
        int? warehouseId,
        int productId,
        BarcodeDraftRequest request,
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

        if (!await db.Products.AnyAsync(x => x.Id == productId, cancellationToken))
        {
            throw new ApiNotFoundException("Номенклатура не найдена.");
        }

        return await CreateBarcodeAsync(userId, BarcodeEntityType.Product, productId, request, cancellationToken);
    }

    public async Task<BarcodeResponse> CreateCellBarcodeAsync(
        int userId,
        int cellId,
        BarcodeDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(cellId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.WarehouseManage,
            cancellationToken);

        return await CreateBarcodeAsync(userId, BarcodeEntityType.Cell, cellId, request, cancellationToken);
    }

    public Task DeactivateAsync(int userId, int barcodeId, CancellationToken cancellationToken = default) =>
        DeactivateAsync(userId, null, barcodeId, cancellationToken);

    public async Task DeactivateAsync(
        int userId,
        int? warehouseId,
        int barcodeId,
        CancellationToken cancellationToken = default)
    {
        var barcode = await db.Barcodes.FirstOrDefaultAsync(x => x.Id == barcodeId, cancellationToken)
            ?? throw new ApiNotFoundException("Штрих-код не найден.");

        await EnsureBarcodeManagementAccessAsync(userId, warehouseId, barcode, cancellationToken);

        if (!barcode.IsActive)
        {
            return;
        }

        var wasPrimary = barcode.IsPrimary;
        barcode.IsActive = false;
        barcode.IsPrimary = false;

        if (wasPrimary)
        {
            var nextPrimary = await db.Barcodes
                .Where(x =>
                    x.Id != barcode.Id &&
                    x.IsActive &&
                    x.EntityType == barcode.EntityType &&
                    x.EntityId == barcode.EntityId)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextPrimary is not null)
            {
                nextPrimary.IsPrimary = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeValue(string value)
    {
        var normalizedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new ApiValidationException("Значение штрих-кода не должно быть пустым.");
        }

        return normalizedValue;
    }

    public static BarcodeSymbology ParseSymbology(string? value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();
        return normalizedValue switch
        {
            null or "" => BarcodeSymbology.Unknown,
            "CODE_128" => BarcodeSymbology.Code128,
            "EAN_13" => BarcodeSymbology.Ean13,
            "EAN_8" => BarcodeSymbology.Ean8,
            "UPC_A" => BarcodeSymbology.UpcA,
            "QR_CODE" => BarcodeSymbology.QrCode,
            "UNKNOWN" => BarcodeSymbology.Unknown,
            _ => BarcodeSymbology.Unknown
        };
    }

    public static string ToApiSymbology(BarcodeSymbology symbology) =>
        symbology switch
        {
            BarcodeSymbology.Code128 => "CODE_128",
            BarcodeSymbology.Ean13 => "EAN_13",
            BarcodeSymbology.Ean8 => "EAN_8",
            BarcodeSymbology.UpcA => "UPC_A",
            BarcodeSymbology.QrCode => "QR_CODE",
            _ => "UNKNOWN"
        };

    private async Task<BarcodeResolveResponse> ResolveProductAsync(
        int userId,
        int? warehouseId,
        string normalizedValue,
        int productId,
        CancellationToken cancellationToken)
    {
        if (warehouseId is null)
        {
            await warehousePermissionService.EnsureAnyWarehouseAccessAsync(userId, cancellationToken);
        }

        var product = await db.Products
            .Where(x => x.Id == productId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Sku
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiConflictException("Штрих-код привязан к удалённому товару.");

        return new BarcodeResolveResponse(
            true,
            normalizedValue,
            BarcodeEntityType.Product.ToString(),
            product.Id,
            product.Name,
            $"Артикул: {product.Sku}");
    }

    private async Task<BarcodeResolveResponse> ResolveCellAsync(
        int userId,
        string normalizedValue,
        int cellId,
        CancellationToken cancellationToken)
    {
        var warehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(cellId, cancellationToken);
        await warehousePermissionService.EnsureWarehousePermissionAsync(
            userId,
            warehouseId,
            WarehousePermission.StockRead,
            cancellationToken);

        var cell = await db.StorageCells
            .Where(x => x.Id == cellId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                WarehouseName = x.StorageZone!.Warehouse!.Name,
                ZoneName = x.StorageZone.Name
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ApiConflictException("Штрих-код привязан к удалённой ячейке.");

        return new BarcodeResolveResponse(
            true,
            normalizedValue,
            BarcodeEntityType.Cell.ToString(),
            cell.Id,
            cell.Code,
            $"Склад: {cell.WarehouseName} / Зона: {cell.ZoneName}");
    }

    private async Task<IReadOnlyList<BarcodeResponse>> GetEntityBarcodesAsync(
        BarcodeEntityType entityType,
        int entityId,
        CancellationToken cancellationToken)
    {
        return await db.Barcodes
            .AsNoTracking()
            .Where(x => x.IsActive && x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new BarcodeResponse(
                x.Id,
                x.Value,
                ToApiSymbology(x.Symbology),
                x.IsPrimary,
                x.IsActive,
                x.CreatedAt,
                x.CreatedByUserId))
            .ToListAsync(cancellationToken);
    }

    private async Task<BarcodeResponse> CreateBarcodeAsync(
        int userId,
        BarcodeEntityType entityType,
        int entityId,
        BarcodeDraftRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedValue = NormalizeValue(request.Value);
        var symbology = ParseSymbology(request.Symbology);

        var existingByValue = await db.Barcodes
            .AsNoTracking()
            .Where(x => x.IsActive && x.Value == normalizedValue)
            .Select(x => new BarcodeLookupRow(x.EntityType, x.EntityId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingByValue is not null)
        {
            var message = existingByValue.EntityType == entityType && existingByValue.EntityId == entityId
                ? "Этот штрих-код уже привязан к выбранному объекту."
                : "Этот штрих-код уже привязан к другому объекту.";
            throw new ApiConflictException(message);
        }

        var entityBarcodes = await db.Barcodes
            .Where(x => x.IsActive && x.EntityType == entityType && x.EntityId == entityId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var shouldBePrimary = request.IsPrimary == true ||
            entityBarcodes.Count == 0 ||
            entityBarcodes.All(x => !x.IsPrimary);

        if (shouldBePrimary)
        {
            foreach (var existingBarcode in entityBarcodes)
            {
                existingBarcode.IsPrimary = false;
            }
        }

        var barcode = new Barcode
        {
            Value = normalizedValue,
            Symbology = symbology,
            EntityType = entityType,
            EntityId = entityId,
            IsPrimary = shouldBePrimary,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId
        };

        db.Barcodes.Add(barcode);
        await db.SaveChangesAsync(cancellationToken);

        return new BarcodeResponse(
            barcode.Id,
            barcode.Value,
            ToApiSymbology(barcode.Symbology),
            barcode.IsPrimary,
            barcode.IsActive,
            barcode.CreatedAt,
            barcode.CreatedByUserId);
    }

    private async Task EnsureBarcodeManagementAccessAsync(
        int userId,
        int? warehouseId,
        Barcode barcode,
        CancellationToken cancellationToken)
    {
        switch (barcode.EntityType)
        {
            case BarcodeEntityType.Product:
                if (warehouseId is null)
                {
                    await warehousePermissionService.EnsureProductManagementAccessAsync(userId, cancellationToken);
                }
                else
                {
                    await warehousePermissionService.EnsureProductManagementAccessAsync(userId, warehouseId.Value, cancellationToken);
                }
                break;

            case BarcodeEntityType.Cell:
            {
                var actualWarehouseId = await warehousePermissionService.GetWarehouseIdForCellAsync(barcode.EntityId, cancellationToken);
                if (warehouseId is not null && actualWarehouseId != warehouseId.Value)
                {
                    throw new ApiConflictException("Штрих-код относится к другому складу.");
                }

                await warehousePermissionService.EnsureWarehousePermissionAsync(
                    userId,
                    actualWarehouseId,
                    WarehousePermission.WarehouseManage,
                    cancellationToken);
                break;
            }

            default:
                throw new ApiValidationException("Не удалось определить права на управление штрих-кодом.");
        }
    }

    private sealed record BarcodeLookupRow(BarcodeEntityType EntityType, int EntityId);
}
