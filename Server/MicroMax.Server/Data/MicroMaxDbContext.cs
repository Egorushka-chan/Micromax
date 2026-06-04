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
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<WarehouseUser> WarehouseUsers => Set<WarehouseUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WarehouseOperation> WarehouseOperations => Set<WarehouseOperation>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<StorageZone>().HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        modelBuilder.Entity<StorageCell>().HasIndex(x => new { x.StorageZoneId, x.Code }).IsUnique();
        modelBuilder.Entity<StockBalance>().HasIndex(x => new { x.ProductId, x.StorageCellId }).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<WarehouseUser>().HasIndex(x => new { x.WarehouseId, x.UserId }).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();

        modelBuilder.Entity<Product>().Property(x => x.MinQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<StockBalance>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<WarehouseOperation>().Property(x => x.Quantity).HasPrecision(18, 3);

        modelBuilder.Entity<WarehouseOperation>()
            .HasOne(x => x.SourceCell)
            .WithMany(x => x.SourceOperations)
            .HasForeignKey(x => x.SourceCellId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WarehouseOperation>()
            .HasOne(x => x.TargetCell)
            .WithMany(x => x.TargetOperations)
            .HasForeignKey(x => x.TargetCellId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Code = SystemRoleCodes.Admin, Name = "Администратор склада" },
            new Role { Id = 2, Code = SystemRoleCodes.Worker, Name = "Сотрудник склада" },
            new Role { Id = 3, Code = SystemRoleCodes.Viewer, Name = "Наблюдатель" });
    }
}
