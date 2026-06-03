using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Micromax.Server.Tests;

public sealed class WarehouseOperationServiceTests
{
    [Fact]
    public async Task AdjustIncreasesBalanceToTargetQuantity()
    {
        await using var db = CreateDb();
        SeedSingleBalance(db, quantity: 3m);
        var service = new WarehouseOperationService(db);

        var operation = await service.AdjustAsync(new AdjustRequest(1, 1, 10m, null, "Проверка"));

        var balance = await db.StockBalances.SingleAsync();
        Assert.Equal(10m, balance.Quantity);
        Assert.Equal(WarehouseOperationType.Adjust, operation.Type);
        Assert.Equal(7m, operation.Quantity);
        Assert.Contains("Корректировка до 10", operation.Comment);
    }

    [Fact]
    public async Task AdjustDecreasesBalanceToTargetQuantity()
    {
        await using var db = CreateDb();
        SeedSingleBalance(db, quantity: 15m);
        var service = new WarehouseOperationService(db);

        var operation = await service.AdjustAsync(new AdjustRequest(1, 1, 4m, null, null));

        var balance = await db.StockBalances.SingleAsync();
        Assert.Equal(4m, balance.Quantity);
        Assert.Equal(11m, operation.Quantity);
    }

    [Fact]
    public async Task AdjustToZeroIsAllowed()
    {
        await using var db = CreateDb();
        SeedSingleBalance(db, quantity: 8m);
        var service = new WarehouseOperationService(db);

        await service.AdjustAsync(new AdjustRequest(1, 1, 0m, null, null));

        var balance = await db.StockBalances.SingleAsync();
        Assert.Equal(0m, balance.Quantity);
    }

    [Fact]
    public async Task AdjustWithoutActualChangeThrows()
    {
        await using var db = CreateDb();
        SeedSingleBalance(db, quantity: 8m);
        var service = new WarehouseOperationService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustAsync(new AdjustRequest(1, 1, 8m, null, null)));

        Assert.Equal("Текущий остаток уже соответствует указанному значению.", error.Message);
    }

    [Fact]
    public async Task AdjustCreatesOperationAndLog()
    {
        await using var db = CreateDb();
        SeedSingleBalance(db, quantity: 2m);
        var service = new WarehouseOperationService(db);

        await service.AdjustAsync(new AdjustRequest(1, 1, 5m, null, null));

        var operation = await db.WarehouseOperations.SingleAsync();
        var log = await db.OperationLogs.SingleAsync();

        Assert.Equal(WarehouseOperationType.Adjust, operation.Type);
        Assert.Equal(3m, operation.Quantity);
        Assert.Equal(operation.Id, log.WarehouseOperationId);
        Assert.Contains("с 2", log.Message);
        Assert.Contains("до 5", log.Message);
    }

    private static MicroMaxDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MicroMaxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MicroMaxDbContext(options);
    }

    private static void SeedSingleBalance(MicroMaxDbContext db, decimal quantity)
    {
        var warehouse = new Warehouse { Id = 1, Name = "Микросклад" };
        var zone = new StorageZone { Id = 1, Warehouse = warehouse, Code = "A", Name = "Зона A" };
        var cell = new StorageCell { Id = 1, StorageZone = zone, Code = "A-1", Name = "Полка A-1" };
        var product = new Product { Id = 1, Sku = "GLV-001", Name = "Перчатки рабочие", Unit = "пар", MinQuantity = 1 };

        db.AddRange(warehouse, zone, cell, product);
        db.StockBalances.Add(new StockBalance
        {
            ProductId = product.Id,
            StorageCellId = cell.Id,
            Quantity = quantity
        });
        db.SaveChanges();
    }
}
