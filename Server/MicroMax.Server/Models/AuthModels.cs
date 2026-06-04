using System.ComponentModel.DataAnnotations;

namespace MicroMax.Server.Models;

public static class SystemRoleCodes
{
    public const string Admin = "ADMIN";
    public const string Worker = "WORKER";
    public const string Viewer = "VIEWER";
}

public sealed class Role
{
    public int Id { get; set; }

    [Required, MaxLength(32)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public List<WarehouseUser> WarehouseUsers { get; set; } = [];
}

public sealed class WarehouseUser
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum WarehousePermission
{
    WarehouseRead = 1,
    WarehouseManage = 2,
    ProductRead = 3,
    ProductManage = 4,
    StockRead = 5,
    OperationsExecute = 6,
    OperationsReadJournal = 7,
    UsersManage = 8
}
