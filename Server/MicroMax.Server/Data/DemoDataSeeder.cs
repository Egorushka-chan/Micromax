using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Data;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(MicroMaxDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Warehouses.AnyAsync())
        {
            return;
        }

        var admin = new UserRole { Name = "Администратор" };
        var operatorRole = new UserRole { Name = "Оператор склада" };
        var user = new AppUser { Login = "admin", DisplayName = "Администратор MicroMax", UserRole = admin };

        var warehouse = new Warehouse { Name = "Микросклад", Address = "Демо-склад" };
        var zoneA = new StorageZone { Warehouse = warehouse, Code = "A", Name = "Основная зона" };
        var zoneB = new StorageZone { Warehouse = warehouse, Code = "B", Name = "Резервная зона" };
        var cellA1 = new StorageCell { StorageZone = zoneA, Code = "A-1", Name = "Полка A-1" };
        var cellA2 = new StorageCell { StorageZone = zoneA, Code = "A-2", Name = "Полка A-2" };
        var cellB1 = new StorageCell { StorageZone = zoneB, Code = "B-1", Name = "Полка B-1" };

        var gloves = new Product { Sku = "GLV-001", Name = "Перчатки рабочие", Unit = "пар", MinQuantity = 10 };
        var tape = new Product { Sku = "TAPE-001", Name = "Скотч упаковочный", Unit = "шт", MinQuantity = 5 };
        var bolts = new Product { Sku = "BOLT-001", Name = "Болт М8", Unit = "шт", MinQuantity = 100 };

        db.AddRange(admin, operatorRole, user, warehouse, zoneA, zoneB, cellA1, cellA2, cellB1, gloves, tape, bolts);
        db.StockBalances.AddRange(
            new StockBalance { Product = gloves, StorageCell = cellA1, Quantity = 25 },
            new StockBalance { Product = tape, StorageCell = cellA2, Quantity = 12 },
            new StockBalance { Product = bolts, StorageCell = cellB1, Quantity = 250 }
        );

        await db.SaveChangesAsync();
    }
}
