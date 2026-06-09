using MicroMax.Server.Api.WarehouseSetup;
using MicroMax.Server.Api.Warehouses;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services.Api;

public sealed class WarehouseSetupService(
    Data.MicroMaxDbContext db,
    WarehousePermissionService warehousePermissionService)
{
    private static readonly IReadOnlyList<WarehouseTemplateDefinition> Templates =
    [
        new(
            "EMPTY",
            "Пустой склад",
            "Создаёт склад без зон и ячеек.",
            []),
        new(
            "CONSUMABLES",
            "Склад расходников",
            "Базовая структура для расходных материалов и быстрого доступа.",
            [
                new WarehouseZoneTemplate("A", "Основное хранение", ["A-01", "A-02", "A-03"]),
                new WarehouseZoneTemplate("B", "Быстрый доступ", ["B-01", "B-02"]),
                new WarehouseZoneTemplate("C", "Списание / брак", ["C-01"])
            ]),
        new(
            "HOME",
            "Домашний склад",
            "Простая структура для домашнего хранения.",
            [
                new WarehouseZoneTemplate("K", "Кухня", ["K-01", "K-02"]),
                new WarehouseZoneTemplate("V", "Ванная", ["V-01", "V-02"]),
                new WarehouseZoneTemplate("H", "Хозяйственные вещи", ["H-01", "H-02"])
            ])
    ];

    public IReadOnlyList<WarehouseSetupTemplateResponse> GetTemplates() =>
        Templates
            .Select(template => new WarehouseSetupTemplateResponse(
                template.Code,
                template.Name,
                template.Description,
                template.Zones.Count,
                template.Zones.Sum(zone => zone.CellCodes.Count)))
            .ToList();

    public async Task<WarehouseSetupResponse> CreateAsync(
        int userId,
        CreateWarehouseFromTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var warehouseName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(warehouseName))
        {
            throw new MicroMax.Server.Infrastructure.Api.ApiValidationException("Укажите название склада.");
        }

        var template = GetRequiredTemplate(request.TemplateCode);
        var adminRole = await warehousePermissionService.GetRequiredRoleAsync(SystemRoleCodes.Admin, cancellationToken);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var warehouse = new Warehouse
        {
            Name = warehouseName,
            Address = NormalizeOptional(request.Address)
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouse.Id,
            UserId = userId,
            RoleId = adminRole.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var zonesCreated = 0;
        var cellsCreated = 0;

        foreach (var zoneTemplate in template.Zones)
        {
            var zone = new StorageZone
            {
                WarehouseId = warehouse.Id,
                Code = zoneTemplate.Code,
                Name = zoneTemplate.Name
            };
            db.StorageZones.Add(zone);
            await db.SaveChangesAsync(cancellationToken);
            zonesCreated += 1;

            foreach (var cellCode in zoneTemplate.CellCodes)
            {
                db.StorageCells.Add(new StorageCell
                {
                    StorageZoneId = zone.Id,
                    Code = cellCode,
                    Name = $"Ячейка {cellCode}"
                });
                cellsCreated += 1;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var structure = await GetStructureAsync(userId, warehouse.Id, cancellationToken);
        return new WarehouseSetupResponse(
            warehouse.Id,
            warehouse.Name,
            structure.RoleCode,
            structure.RoleName,
            zonesCreated,
            cellsCreated,
            structure);
    }

    private async Task<WarehouseStructureResponse> GetStructureAsync(
        int userId,
        int warehouseId,
        CancellationToken cancellationToken)
    {
        var context = await warehousePermissionService.GetRequiredUserWarehouseContextAsync(userId, warehouseId, cancellationToken);
        var warehouse = await db.Warehouses
            .Where(x => x.Id == warehouseId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                Zones = x.Zones
                    .OrderBy(zone => zone.Code)
                    .Select(zone => new WarehouseStructureZoneResponse(
                        zone.Id,
                        zone.Code,
                        zone.Name,
                        zone.Cells
                            .OrderBy(cell => cell.Code)
                            .Select(cell => new WarehouseStructureCellResponse(cell.Id, cell.Code, cell.Name))
                            .ToList()))
                    .ToList()
            })
            .FirstAsync(cancellationToken);

        return new WarehouseStructureResponse(
            warehouse.Id,
            warehouse.Name,
            warehouse.Address,
            context.RoleCode,
            context.RoleName,
            warehouse.Zones);
    }

    private static WarehouseTemplateDefinition GetRequiredTemplate(string templateCode)
    {
        var normalizedCode = templateCode.Trim().ToUpperInvariant();
        return Templates.FirstOrDefault(template => template.Code == normalizedCode)
            ?? throw new MicroMax.Server.Infrastructure.Api.ApiValidationException("Указанный шаблон склада не поддерживается.");
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record WarehouseTemplateDefinition(
        string Code,
        string Name,
        string Description,
        IReadOnlyList<WarehouseZoneTemplate> Zones);

    private sealed record WarehouseZoneTemplate(
        string Code,
        string Name,
        IReadOnlyList<string> CellCodes);
}
