using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;

namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Rule-based fallback: работает без внешней модели и возвращает тот же контракт, что реальные ИИ-провайдеры.
/// </summary>
public sealed class MockAiCommandProvider(AiCommandRules commandRules) : IAiCommandProvider
{
    public AiProviderKind Kind => AiProviderKind.Mock;
    public bool IsRealProvider => false;

    public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        var lower = text.Trim().ToLowerInvariant();
        var type = commandRules.DetectCommandType(lower);
        var cells = AiCommandRules.FindCells(lower, context.Cells);
        var productMatches = AiCommandRules.FindProducts(lower, context.Products);

        if (commandRules.RequiresProduct(type) && productMatches.Count > 1)
        {
            return Task.FromResult(Clarification(
                "Найдено несколько товаров. Уточните, какой товар нужен.",
                productMatches.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList()));
        }

        if (commandRules.RequiresProduct(type) && productMatches.Count == 0)
        {
            return Task.FromResult(Clarification("Не удалось определить товар. Укажите название или SKU.", []));
        }

        var textWithoutCells = cells.Aggregate(lower, (current, cell) => current.Replace(cell.Code.ToLowerInvariant(), " "));
        var quantity = AiCommandRules.ReadNumber(textWithoutCells);
        var minQuantity = AiCommandRules.ReadMinQuantity(lower) ?? quantity;
        var sku = AiCommandRules.MatchValue(text, @"(?:sku|артикул)\s*[:\-]?\s*([A-Za-zА-Яа-я0-9_-]+)");
        var name = AiCommandRules.MatchValue(text, @"(?:создай|создать|добавь|добавить)\s+товар\s+(.+?)(?:\s+(?:sku|артикул|мин|минимальный|минимум)\b|$)")?.Trim();

        var command = new AssistantCommand
        {
            Mode = type == "unknown" ? "Unknown" : "Command",
            Provider = Kind.ToString(),
            CommandType = type,
            ProductId = productMatches.SingleOrDefault()?.Id,
            SourceCellId = cells.FirstOrDefault()?.Id,
            TargetCellId = type switch
            {
                "move_product" => cells.Skip(1).FirstOrDefault()?.Id,
                "post_receipt" => cells.FirstOrDefault()?.Id,
                _ => null
            },
            Quantity = quantity,
            MinQuantity = minQuantity,
            Sku = sku,
            Name = name,
            Unit = lower.Contains("кг") ? "кг" : "шт"
        };
        command.RiskLevel = commandRules.RiskFor(command.CommandType);
        command.RequiresConfirmation = command.RiskLevel is "Medium" or "High" or "Critical";
        command.Summary = commandRules.BuildSummary(command, context);

        return Task.FromResult(command);
    }

    private AssistantCommand Clarification(string question, List<AssistantChoice> choices)
    {
        return new AssistantCommand
        {
            Mode = "Clarification",
            Provider = Kind.ToString(),
            CommandType = "unknown",
            RiskLevel = "None",
            RequiresConfirmation = false,
            Summary = question,
            ClarificationQuestion = question,
            Choices = choices
        };
    }
}
