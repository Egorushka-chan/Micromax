using System.Globalization;
using System.Text.RegularExpressions;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Assistant.Core;

/// <summary>
/// Набор rule-based правил: используется Mock-провайдером и серверной валидацией, но список команд берёт из реестра.
/// </summary>
public sealed class AiCommandRules(AiCommandRegistry commandRegistry)
{
    private static readonly HashSet<string> StopWords =
    [
        "найди", "найти", "где", "лежит", "лежат", "покажи", "товар", "товары",
        "создай", "создать", "добавь", "добавить", "перемести", "переместить",
        "перенеси", "спиши", "списать", "проведи", "провести", "поступление",
        "остаток", "остатка", "минимальный", "минимум", "sku", "артикул"
    ];

    public string DetectCommandType(string lower)
    {
        return commandRegistry.Commands.FirstOrDefault(command => HasAny(lower, command.TriggerPhrases.ToArray()))?.Type
            ?? AiCommandRegistry.Unknown;
    }

    public string RiskFor(string commandType) => commandRegistry.Find(commandType)?.RiskLevel ?? "None";

    public bool RequiresProduct(string commandType) => commandRegistry.Find(commandType)?.RequiresProduct == true;

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

    public string BuildSummary(AssistantCommand command, AiCommandContext context)
    {
        var product = context.Products.FirstOrDefault(x => x.Id == command.ProductId);
        var source = context.Cells.FirstOrDefault(x => x.Id == command.SourceCellId);
        var target = context.Cells.FirstOrDefault(x => x.Id == command.TargetCellId);
        var definition = commandRegistry.Find(command.CommandType);

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
            _ => definition?.Title ?? "Команда не распознана."
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
