using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Data;

public sealed class MicroMaxDbContext(DbContextOptions<MicroMaxDbContext> options) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StorageZone> StorageZones => Set<StorageZone>();
    public DbSet<StorageCell> StorageCells => Set<StorageCell>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<WarehouseOperation> WarehouseOperations => Set<WarehouseOperation>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<StorageZone>().HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        modelBuilder.Entity<StorageCell>().HasIndex(x => new { x.StorageZoneId, x.Code }).IsUnique();
        modelBuilder.Entity<StockBalance>().HasIndex(x => new { x.ProductId, x.StorageCellId }).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Login).IsUnique();

        modelBuilder.Entity<Product>().Property(x => x.MinQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<StockBalance>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<WarehouseOperation>().Property(x => x.Quantity).HasPrecision(18, 3);
    }
}
