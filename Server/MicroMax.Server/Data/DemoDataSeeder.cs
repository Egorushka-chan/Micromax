using MicroMax.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Data;

public static class DemoDataSeeder
{
    private const string DemoAdminEmail = "admin@micromax.local";
    private const string DemoAdminPassword = "Admin12345!";

    public static async Task SeedAsync(MicroMaxDbContext db, IPasswordHasher<AppUser> passwordHasher)
    {
        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Code = SystemRoleCodes.Admin, Name = "Администратор склада" },
                new Role { Code = SystemRoleCodes.Worker, Name = "Сотрудник склада" },
                new Role { Code = SystemRoleCodes.Viewer, Name = "Наблюдатель" });
            await db.SaveChangesAsync();
        }

        if (await db.Warehouses.AnyAsync())
        {
            return;
        }

        var adminRole = await db.Roles.FirstAsync(x => x.Code == SystemRoleCodes.Admin);
        var user = new AppUser
        {
            Email = DemoAdminEmail,
            DisplayName = "Администратор MicroMax",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DemoAdminPassword);

        var warehouse = new Warehouse { Name = "Микросклад", Address = "Демо-склад" };
        var zoneA = new StorageZone { Warehouse = warehouse, Code = "A", Name = "Основная зона" };
        var zoneB = new StorageZone { Warehouse = warehouse, Code = "B", Name = "Резервная зона" };
        var cellA1 = new StorageCell { StorageZone = zoneA, Code = "A-1", Name = "Полка A-1" };
        var cellA2 = new StorageCell { StorageZone = zoneA, Code = "A-2", Name = "Полка A-2" };
        var cellB1 = new StorageCell { StorageZone = zoneB, Code = "B-1", Name = "Полка B-1" };

        var gloves = new Product { Sku = "GLV-001", Name = "Перчатки рабочие", Unit = "пар", MinQuantity = 10 };
        var tape = new Product { Sku = "TAPE-001", Name = "Скотч упаковочный", Unit = "шт", MinQuantity = 5 };
        var bolts = new Product { Sku = "BOLT-001", Name = "Болт М8", Unit = "шт", MinQuantity = 100 };

        db.AddRange(user, warehouse, zoneA, zoneB, cellA1, cellA2, cellB1, gloves, tape, bolts);
        db.StockBalances.AddRange(
            new StockBalance { Product = gloves, StorageCell = cellA1, Quantity = 25 },
            new StockBalance { Product = tape, StorageCell = cellA2, Quantity = 12 },
            new StockBalance { Product = bolts, StorageCell = cellB1, Quantity = 250 });
        await db.SaveChangesAsync();

        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouse.Id,
            UserId = user.Id,
            RoleId = adminRole.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return;

#if false

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
#endif
    }
}
