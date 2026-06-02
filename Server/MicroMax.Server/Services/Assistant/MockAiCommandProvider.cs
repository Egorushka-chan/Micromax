using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant;

public sealed class MockAiCommandProvider : IAiCommandProvider
{
    public AiProviderKind Kind => AiProviderKind.Mock;
    public bool IsRealProvider => false;

    public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
    {
        var lower = text.Trim().ToLowerInvariant();
        var type = AiCommandRules.DetectCommandType(lower);
        var cells = AiCommandRules.FindCells(lower, context.Cells);
        var productMatches = AiCommandRules.FindProducts(lower, context.Products);

        if (AiCommandRules.RequiresProduct(type) && productMatches.Count > 1)
        {
            return Task.FromResult(Clarification(
                "Найдено несколько товаров. Уточните, какой товар нужен.",
                productMatches.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList()));
        }

        if (AiCommandRules.RequiresProduct(type) && productMatches.Count == 0)
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
        command.RiskLevel = AiCommandRules.RiskFor(command.CommandType);
        command.RequiresConfirmation = command.RiskLevel is "Medium" or "High" or "Critical";
        command.Summary = AiCommandRules.BuildSummary(command, context);

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
