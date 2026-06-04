using System.ComponentModel.DataAnnotations;

namespace MicroMax.Server.Models;

public enum BarcodeEntityType
{
    Product = 1,
    Cell = 2
}

public enum BarcodeSymbology
{
    Unknown = 0,
    Code128 = 1,
    Ean13 = 2,
    Ean8 = 3,
    UpcA = 4,
    QrCode = 5
}

/// <summary>
/// Физический штрих-код, привязанный к товару или ячейке хранения.
/// </summary>
public sealed class Barcode
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Value { get; set; } = string.Empty;

    public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Unknown;

    public BarcodeEntityType EntityType { get; set; }

    public int EntityId { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int CreatedByUserId { get; set; }

    public AppUser? CreatedByUser { get; set; }
}
