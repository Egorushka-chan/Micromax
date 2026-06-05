using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;

namespace MicroMax.Server.Services.Assistant.Providers;

/// <summary>
/// Rule-based fallback. Работает без внешней модели и повторяет базовые
/// сценарии помощника, пока недоступны реальные AI-провайдеры.
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
        var textWithoutCells = cells.Aggregate(lower, static (current, cell) => current.Replace(cell.Code.ToLowerInvariant(), " "));
        var quantity = AiCommandRules.ReadNumber(textWithoutCells);
        var minQuantity = AiCommandRules.ReadMinQuantity(lower) ?? quantity;
        var sku = AiCommandRules.MatchValue(text, @"(?:sku|артикул)\s*[:\-]?\s*([\p{L}\p{N}_\-]+)");
        var name = AiCommandRules.MatchValue(
            text,
            @"(?:создай|создать|добавь|добавить)\s+товар\s+(.+?)(?:\s+(?:sku|артикул|мин|минимальный|минимум)\b|$)")?.Trim();

        var command = new AssistantCommand
        {
            Mode = type == "unknown" ? "Unknown" : "Command",
            Provider = Kind.ToString(),
            CommandType = type,
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

        var productMatches = AiCommandRules.FindProducts(lower, context.Products);
        if (productMatches.Count == 1)
        {
            command.ProductId = productMatches[0].Id;
        }

        if (commandRules.RequiresProduct(type) && productMatches.Count > 1)
        {
            return Task.FromResult(Clarification(
                command,
                "Найдено несколько товаров. Уточните, какой товар нужен.",
                productMatches.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList(),
                "Product"));
        }

        if (commandRules.RequiresProduct(type) && productMatches.Count == 0)
        {
            return Task.FromResult(Clarification(
                command,
                "Не удалось определить товар. Укажите название или SKU.",
                [],
                "Product"));
        }

        command.RiskLevel = commandRules.RiskFor(command.CommandType);
        command.RequiresConfirmation = command.RiskLevel is "Medium" or "High" or "Critical";
        command.Summary = commandRules.BuildSummary(command, context);

        return Task.FromResult(command);
    }

    private AssistantCommand Clarification(
        AssistantCommand command,
        string question,
        List<AssistantChoice> choices,
        string? clarificationTarget)
    {
        command.Mode = "Clarification";
        command.Provider = Kind.ToString();
        command.RiskLevel = "None";
        command.RequiresConfirmation = false;
        command.Summary = question;
        command.ClarificationQuestion = question;
        command.ClarificationTarget = clarificationTarget;
        command.Choices = choices;
        return command;
    }
}
