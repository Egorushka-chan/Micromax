using System.ComponentModel.DataAnnotations;

namespace MicroMax.Server.Models;

public enum WarehouseOperationType
{
    Receive = 1,
    Move = 2,
    WriteOff = 3,
    Adjust = 4
}

public sealed class Warehouse
{
    public int Id { get; set; }
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(240)]
    public string? Address { get; set; }
    public List<StorageZone> Zones { get; set; } = [];
}

public sealed class StorageZone
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    [Required, MaxLength(32)]
    public string Code { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    public List<StorageCell> Cells { get; set; } = [];
}

public sealed class StorageCell
{
    public int Id { get; set; }
    public int StorageZoneId { get; set; }
    public StorageZone? StorageZone { get; set; }
    [Required, MaxLength(32)]
    public string Code { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    public List<StockBalance> Balances { get; set; } = [];
}

public sealed class Product
{
    public int Id { get; set; }
    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(24)]
    public string Unit { get; set; } = "шт";
    public decimal MinQuantity { get; set; }
    public List<StockBalance> Balances { get; set; } = [];
}

public sealed class StockBalance
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int StorageCellId { get; set; }
    public StorageCell? StorageCell { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class UserRole
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;
    public List<AppUser> Users { get; set; } = [];
}

public sealed class AppUser
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string Login { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;
    public int UserRoleId { get; set; }
    public UserRole? UserRole { get; set; }
}

public sealed class WarehouseOperation
{
    public int Id { get; set; }
    public WarehouseOperationType Type { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? SourceCellId { get; set; }
    public StorageCell? SourceCell { get; set; }
    public int? TargetCellId { get; set; }
    public StorageCell? TargetCell { get; set; }
    public int? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public decimal Quantity { get; set; }
    [MaxLength(500)]
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<OperationLog> Logs { get; set; } = [];
}

public sealed class OperationLog
{
    public int Id { get; set; }
    public int WarehouseOperationId { get; set; }
    public WarehouseOperation? WarehouseOperation { get; set; }
    [Required, MaxLength(600)]
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record ReceiveRequest(int ProductId, int TargetCellId, decimal Quantity, int? UserId, string? Comment);
public sealed record MoveRequest(int ProductId, int SourceCellId, int TargetCellId, decimal Quantity, int? UserId, string? Comment);
public sealed record WriteOffRequest(int ProductId, int SourceCellId, decimal Quantity, int? UserId, string? Comment);
public sealed record AdjustRequest(int ProductId, int TargetCellId, decimal TargetQuantity, int? UserId, string? Comment);
public sealed record AssistantRequest(string Text);
public sealed record AssistantConfirmationRequest(string CommandId, bool Confirmed);

public sealed class AssistantCommand
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
    public string Mode { get; set; } = "Command";
    public string Provider { get; set; } = "Mock";
    public string CommandType { get; set; } = "unknown";
    public string RiskLevel { get; set; } = "None";
    public int? ProductId { get; set; }
    public int? SourceCellId { get; set; }
    public int? TargetCellId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? MinQuantity { get; set; }
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public string? Unit { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? ClarificationQuestion { get; set; }
    public List<AssistantChoice> Choices { get; set; } = [];
}

public sealed record AssistantCommandResult(bool Success, string Message, IReadOnlyList<string> Details);
public sealed record AssistantChoice(string Id, string Label, string Kind);
