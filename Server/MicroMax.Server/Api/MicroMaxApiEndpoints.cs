using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Assistant.Registry;
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

        api.MapGet("/assistant/commands", (AiCommandRegistry registry) => Results.Ok(registry.Commands));

        api.MapPost("/assistant/interpret", async (AssistantRequest request, AssistantService service, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.InterpretAsync(request.Text, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Results.Empty;
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
                var operation = await ExecuteAssistantCommandAsync(command, operations, db);

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

    private static Task<WarehouseOperation?> ExecuteAssistantCommandAsync(
        AssistantCommand command,
        WarehouseOperationService operations,
        MicroMaxDbContext db) => command.CommandType switch
        {
            "post_receipt" => ReceiveFromCommandAsync(command, operations),
            "move_product" => MoveFromCommandAsync(command, operations),
            "write_off_product" => WriteOffFromCommandAsync(command, operations),
            "create_product" => CreateProductFromCommandAsync(command, db),
            "update_min_stock" => UpdateMinQuantityFromCommandAsync(command, db),
            _ => throw new InvalidOperationException("Команда не содержит достаточных данных для выполнения.")
        };

    private static async Task<WarehouseOperation?> ReceiveFromCommandAsync(AssistantCommand command, WarehouseOperationService operations)
    {
        Ensure(command.ProductId, command.TargetCellId, command.Quantity);
        return await operations.ReceiveAsync(new ReceiveRequest(command.ProductId!.Value, command.TargetCellId!.Value, command.Quantity!.Value, null, command.Summary));
    }

    private static async Task<WarehouseOperation?> MoveFromCommandAsync(AssistantCommand command, WarehouseOperationService operations)
    {
        Ensure(command.ProductId, command.SourceCellId, command.TargetCellId, command.Quantity);
        return await operations.MoveAsync(new MoveRequest(command.ProductId!.Value, command.SourceCellId!.Value, command.TargetCellId!.Value, command.Quantity!.Value, null, command.Summary));
    }

    private static async Task<WarehouseOperation?> WriteOffFromCommandAsync(AssistantCommand command, WarehouseOperationService operations)
    {
        Ensure(command.ProductId, command.SourceCellId, command.Quantity);
        return await operations.WriteOffAsync(new WriteOffRequest(command.ProductId!.Value, command.SourceCellId!.Value, command.Quantity!.Value, null, command.Summary));
    }

    private static void Ensure(params object?[] values)
    {
        if (values.Any(x => x is null))
        {
            throw new InvalidOperationException("Команда не содержит достаточных данных для выполнения.");
        }
    }
}
