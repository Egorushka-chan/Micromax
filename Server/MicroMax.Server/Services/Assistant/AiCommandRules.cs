using System.Globalization;
using System.Text.RegularExpressions;
using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant;

public static class AiCommandRules
{
    private static readonly HashSet<string> StopWords =
    [
        "найди", "найти", "где", "лежит", "лежат", "покажи", "товар", "товары",
        "создай", "создать", "добавь", "добавить", "перемести", "переместить",
        "перенеси", "спиши", "списать", "проведи", "провести", "поступление",
        "остаток", "остатка", "минимальный", "минимум", "sku", "артикул"
    ];

    public static string DetectCommandType(string lower)
    {
        if (HasAny(lower, "отмени", "отменить", "стоп")) return "cancel";
        if (HasAny(lower, "что ты умеешь", "доступные команды", "помощь", "help")) return "help";
        if (HasAny(lower, "спис", "спиш", "расход")) return "write_off_product";
        if (HasAny(lower, "перемест", "перенеси")) return "move_product";
        if (HasAny(lower, "проведи поступ", "провести поступ", "прими", "приём", "прием")) return "post_receipt";
        if (HasAny(lower, "создай поступ", "создать поступ", "черновик поступ")) return "create_receipt";
        if (HasAny(lower, "создай товар", "создать товар", "добавь товар", "добавить товар")) return "create_product";
        if (HasAny(lower, "минимальн", "минимум", "мин остат")) return "update_min_stock";
        if (HasAny(lower, "нулев", "нет остат")) return "zero_stock";
        if (HasAny(lower, "низк", "мало", "заканч")) return "low_stock";
        if (HasAny(lower, "сводк", "итоги", "статист")) return "warehouse_summary";
        if (HasAny(lower, "открой список товаров", "список товаров", "номенклатур")) return "open_products";
        if (HasAny(lower, "найди", "найти", "где леж", "покажи товар")) return "find_product";
        return "unknown";
    }

    public static string RiskFor(string commandType) => commandType switch
    {
        "create_product" or "update_min_stock" => "Medium",
        "move_product" or "write_off_product" or "post_receipt" => "High",
        "create_receipt" => "Low",
        _ => "None"
    };

    public static bool RequiresProduct(string commandType) => commandType is
        "find_product" or
        "update_min_stock" or
        "move_product" or
        "write_off_product" or
        "create_receipt" or
        "post_receipt";

    public static List<Product> FindProducts(string lower, IReadOnlyList<Product> products)
    {
        var exact = products.Where(product =>
            lower.Contains(product.Sku.ToLowerInvariant()) ||
            lower.Contains(product.Name.ToLowerInvariant())).ToList();
        if (exact.Count > 0)
        {
            return exact;
        }

        var tokens = MeaningfulTokens(lower);
        return products.Where(product =>
            tokens.Any(token =>
                MatchesProductToken(product.Name, token) ||
                MatchesProductToken(product.Sku, token))).ToList();
    }

    public static List<StorageCell> FindCells(string lower, IReadOnlyList<StorageCell> cells)
    {
        return cells
            .Where(cell => lower.Contains(cell.Code.ToLowerInvariant()))
            .OrderBy(cell => lower.IndexOf(cell.Code.ToLowerInvariant(), StringComparison.Ordinal))
            .ToList();
    }

    public static decimal? ReadNumber(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:[,.]\d+)?)");
        return match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static decimal? ReadMinQuantity(string text)
    {
        var match = Regex.Match(text, @"(?:мин(?:имальный)?\s*(?:остаток)?|минимум)\D*(\d+(?:[,.]\d+)?)", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static string? MatchValue(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string BuildSummary(AssistantCommand command, AiCommandContext context)
    {
        var product = context.Products.FirstOrDefault(x => x.Id == command.ProductId);
        var source = context.Cells.FirstOrDefault(x => x.Id == command.SourceCellId);
        var target = context.Cells.FirstOrDefault(x => x.Id == command.TargetCellId);

        return command.CommandType switch
        {
            "open_products" => "Открыть список товаров.",
            "find_product" => product is null ? "Поиск товара." : $"Найти товар: {product.Name} ({product.Sku}).",
            "low_stock" => "Показать товары с низким остатком.",
            "zero_stock" => "Показать товары с нулевым остатком.",
            "warehouse_summary" => "Показать сводку по микроскладу.",
            "create_product" => $"Создать товар «{command.Name}» с SKU {command.Sku}.",
            "update_min_stock" => $"Изменить минимальный остаток для «{product?.Name}» на {command.MinQuantity}.",
            "move_product" => $"Переместить {command.Quantity} товара «{product?.Name}» из {source?.Code} в {target?.Code}.",
            "write_off_product" => $"Списать {command.Quantity} товара «{product?.Name}» из {source?.Code}.",
            "create_receipt" => $"Подготовить черновик поступления для «{product?.Name}».",
            "post_receipt" => $"Провести поступление {command.Quantity} товара «{product?.Name}» в {target?.Code}.",
            "cancel" => "Отменить ожидающую команду.",
            "help" => "Показать доступные команды.",
            _ => "Команда не распознана."
        };
    }

    private static bool HasAny(string text, params string[] tokens) => tokens.Any(text.Contains);

    private static bool MatchesProductToken(string value, string token)
    {
        var lower = value.ToLowerInvariant();
        if (lower.Contains(token))
        {
            return true;
        }

        return MeaningfulTokens(lower).Any(part =>
            part.Length >= 4 && token.Length >= 4 && part[..4] == token[..4]);
    }

    private static List<string> MeaningfulTokens(string text)
    {
        return Regex.Split(text.ToLowerInvariant(), @"[^A-Za-zА-Яа-я0-9_-]+")
            .Where(token => token.Length >= 3 && !StopWords.Contains(token) && !decimal.TryParse(token, out _))
            .ToList();
    }
}
