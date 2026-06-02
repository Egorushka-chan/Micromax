using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Api;

public static class MicroMaxApiEndpoints
{
    public static IEndpointRouteBuilder MapMicroMaxApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/warehouses", async (MicroMaxDbContext db) => await db.Warehouses.OrderBy(x => x.Name).ToListAsync());
        api.MapPost("/warehouses", async (Warehouse item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapPut("/warehouses/{id:int}", async (int id, Warehouse input, MicroMaxDbContext db) =>
        {
            var item = await db.Warehouses.FindAsync(id);
            if (item is null) return Results.NotFound();
            item.Name = input.Name;
            item.Address = input.Address;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });
        api.MapDelete("/warehouses/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<Warehouse>(db, id));

        api.MapGet("/zones", async (MicroMaxDbContext db) => await db.StorageZones.Include(x => x.Warehouse).OrderBy(x => x.Code).ToListAsync());
        api.MapPost("/zones", async (StorageZone item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapDelete("/zones/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<StorageZone>(db, id));

        api.MapGet("/cells", async (MicroMaxDbContext db) => await db.StorageCells.Include(x => x.StorageZone).ThenInclude(x => x!.Warehouse).OrderBy(x => x.Code).ToListAsync());
        api.MapGet("/cells/{id:int}/contents", async (int id, MicroMaxDbContext db) => await db.StockBalances
            .Include(x => x.Product)
            .Where(x => x.StorageCellId == id && x.Quantity > 0)
            .Select(x => new { x.ProductId, ProductName = x.Product!.Name, x.Product!.Sku, x.Quantity, x.Product!.Unit })
            .ToListAsync());
        api.MapPost("/cells", async (StorageCell item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapDelete("/cells/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<StorageCell>(db, id));

        api.MapGet("/products", async (MicroMaxDbContext db) => await db.Products.OrderBy(x => x.Name).ToListAsync());
        api.MapGet("/products/{id:int}/locations", async (int id, MicroMaxDbContext db) => await db.StockBalances
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.ProductId == id && x.Quantity > 0)
            .Select(x => new { CellId = x.StorageCellId, CellCode = x.StorageCell!.Code, ZoneCode = x.StorageCell.StorageZone!.Code, x.Quantity })
            .ToListAsync());
        api.MapPost("/products", async (Product item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapPut("/products/{id:int}", async (int id, Product input, MicroMaxDbContext db) =>
        {
            var item = await db.Products.FindAsync(id);
            if (item is null) return Results.NotFound();
            item.Sku = input.Sku;
            item.Name = input.Name;
            item.Unit = input.Unit;
            item.MinQuantity = input.MinQuantity;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });
        api.MapDelete("/products/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<Product>(db, id));

        api.MapGet("/stocks", GetStocksAsync);
        api.MapGet("/operations", async (DateTimeOffset? from, DateTimeOffset? to, MicroMaxDbContext db) =>
        {
            var query = db.WarehouseOperations
                .Include(x => x.Product)
                .Include(x => x.SourceCell)
                .Include(x => x.TargetCell)
                .OrderByDescending(x => x.CreatedAt)
                .AsQueryable();
            if (from is not null) query = query.Where(x => x.CreatedAt >= from);
            if (to is not null) query = query.Where(x => x.CreatedAt <= to);
            return await query.Take(200).Select(x => new
            {
                x.Id,
                Type = x.Type.ToString(),
                ProductName = x.Product!.Name,
                SourceCell = x.SourceCell == null ? null : x.SourceCell.Code,
                TargetCell = x.TargetCell == null ? null : x.TargetCell.Code,
                x.Quantity,
                x.Comment,
                x.CreatedAt
            }).ToListAsync();
        });

        api.MapGet("/roles", async (MicroMaxDbContext db) => await db.UserRoles.OrderBy(x => x.Name).ToListAsync());
        api.MapPost("/roles", async (UserRole item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapDelete("/roles/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<UserRole>(db, id));

        api.MapGet("/users", async (MicroMaxDbContext db) => await db.AppUsers.Include(x => x.UserRole).OrderBy(x => x.DisplayName).ToListAsync());
        api.MapPost("/users", async (AppUser item, MicroMaxDbContext db) => await CreateAsync(db, item));
        api.MapDelete("/users/{id:int}", async (int id, MicroMaxDbContext db) => await DeleteAsync<AppUser>(db, id));

        api.MapPost("/operations/receive", async (ReceiveRequest request, WarehouseOperationService service) => await RunOperationAsync(() => service.ReceiveAsync(request)));
        api.MapPost("/operations/move", async (MoveRequest request, WarehouseOperationService service) => await RunOperationAsync(() => service.MoveAsync(request)));
        api.MapPost("/operations/write-off", async (WriteOffRequest request, WarehouseOperationService service) => await RunOperationAsync(() => service.WriteOffAsync(request)));

        api.MapPost("/assistant/interpret", async (AssistantRequest request, AssistantService service) =>
        {
            try
            {
                return Results.Ok(await service.InterpretAsync(request.Text));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPost("/assistant/confirm", async (AssistantConfirmationRequest request, WarehouseOperationService operations, MicroMaxDbContext db) =>
        {
            if (!request.Confirmed)
            {
                return Results.Ok(new AssistantCommandResult(true, "Команда отменена.", []));
            }

            if (!AssistantService.TryTakePendingCommand(request.CommandId, out var command) || command is null)
            {
                return Results.NotFound(new { error = "Команда не найдена или уже обработана." });
            }

            try
            {
                var operation = command.CommandType switch
                {
                    "post_receipt" when command.ProductId is not null && command.TargetCellId is not null && command.Quantity is not null =>
                        await operations.ReceiveAsync(new ReceiveRequest(command.ProductId.Value, command.TargetCellId.Value, command.Quantity.Value, null, command.Summary)),
                    "move" when command.ProductId is not null && command.SourceCellId is not null && command.TargetCellId is not null && command.Quantity is not null =>
                        await operations.MoveAsync(new MoveRequest(command.ProductId.Value, command.SourceCellId.Value, command.TargetCellId.Value, command.Quantity.Value, null, command.Summary)),
                    "move_product" when command.ProductId is not null && command.SourceCellId is not null && command.TargetCellId is not null && command.Quantity is not null =>
                        await operations.MoveAsync(new MoveRequest(command.ProductId.Value, command.SourceCellId.Value, command.TargetCellId.Value, command.Quantity.Value, null, command.Summary)),
                    "write_off" when command.ProductId is not null && command.SourceCellId is not null && command.Quantity is not null =>
                        await operations.WriteOffAsync(new WriteOffRequest(command.ProductId.Value, command.SourceCellId.Value, command.Quantity.Value, null, command.Summary)),
                    "write_off_product" when command.ProductId is not null && command.SourceCellId is not null && command.Quantity is not null =>
                        await operations.WriteOffAsync(new WriteOffRequest(command.ProductId.Value, command.SourceCellId.Value, command.Quantity.Value, null, command.Summary)),
                    "create_product" when !string.IsNullOrWhiteSpace(command.Sku) && !string.IsNullOrWhiteSpace(command.Name) =>
                        await CreateProductFromCommandAsync(command, db),
                    "update_min_stock" when command.ProductId is not null && command.MinQuantity is not null =>
                        await UpdateMinQuantityFromCommandAsync(command, db),
                    _ => throw new InvalidOperationException("Команда не содержит достаточных данных для выполнения.")
                };

                return Results.Ok(new AssistantCommandResult(
                    true,
                    "Команда подтверждена и выполнена.",
                    [operation is null ? command.Summary : $"Операция #{operation.Id}: {operation.Type}"]
                ));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    private static async Task<IResult> GetStocksAsync(MicroMaxDbContext db)
    {
        var rows = await db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.StorageCell)
            .ThenInclude(x => x!.StorageZone)
            .Where(x => x.Quantity > 0)
            .OrderBy(x => x.Product!.Name)
            .ThenBy(x => x.StorageCell!.Code)
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product!.Name,
                x.Product.Sku,
                x.Product.Unit,
                CellId = x.StorageCellId,
                CellCode = x.StorageCell!.Code,
                ZoneCode = x.StorageCell.StorageZone!.Code,
                x.Quantity
            })
            .ToListAsync();

        return Results.Ok(rows);
    }

    private static async Task<IResult> RunOperationAsync(Func<Task<WarehouseOperation>> action)
    {
        try
        {
            var operation = await action();
            return Results.Ok(new { operation.Id, operation.Type, operation.Quantity, operation.CreatedAt });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> CreateAsync<T>(MicroMaxDbContext db, T item) where T : class
    {
        db.Set<T>().Add(item);
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    private static async Task<IResult> DeleteAsync<T>(MicroMaxDbContext db, int id) where T : class
    {
        var item = await db.Set<T>().FindAsync(id);
        if (item is null)
        {
            return Results.NotFound();
        }

        db.Remove(item);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<WarehouseOperation?> CreateProductFromCommandAsync(AssistantCommand command, MicroMaxDbContext db)
    {
        db.Products.Add(new Product
        {
            Sku = command.Sku!.Trim(),
            Name = command.Name!.Trim(),
            Unit = string.IsNullOrWhiteSpace(command.Unit) ? "шт" : command.Unit.Trim(),
            MinQuantity = command.MinQuantity ?? 0
        });
        await db.SaveChangesAsync();
        return null;
    }

    private static async Task<WarehouseOperation?> UpdateMinQuantityFromCommandAsync(AssistantCommand command, MicroMaxDbContext db)
    {
        var product = await db.Products.FindAsync(command.ProductId!.Value)
            ?? throw new InvalidOperationException("Номенклатура не найдена.");
        product.MinQuantity = command.MinQuantity!.Value;
        await db.SaveChangesAsync();
        return null;
    }
}
