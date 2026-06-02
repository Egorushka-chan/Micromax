using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Assistant.Execution;

/// <summary>
/// Приводит ответ ИИ к серверному контракту и отсекает неполные или небезопасные команды.
/// </summary>
public sealed class AiCommandNormalizer(AiCommandRegistry commandRegistry, AiCommandRules commandRules)
{
    public AssistantCommand Normalize(AssistantCommand command, AiCommandContext context, AiProviderKind provider)
    {
        command.CommandId = string.IsNullOrWhiteSpace(command.CommandId) ? Guid.NewGuid().ToString("N") : command.CommandId;
        command.Provider = provider.ToString();
        command.Mode = NormalizeMode(command.Mode, command.CommandType);
        command.CommandType = commandRegistry.NormalizeType(command.CommandType);
        command.RiskLevel = commandRules.RiskFor(command.CommandType);
        command.RequiresConfirmation = command.Mode == "Command" && command.RiskLevel is "Medium" or "High" or "Critical";

        if (command.Mode == "Clarification")
        {
            command.CommandType = "unknown";
            command.RiskLevel = "None";
            command.RequiresConfirmation = false;
            command.Summary = command.ClarificationQuestion ?? command.Summary;
            return command;
        }

        var validation = Validate(command, context);
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(command.Summary))
        {
            command.Summary = commandRules.BuildSummary(command, context);
        }

        return command;
    }

    private static string NormalizeMode(string? mode, string? commandType)
    {
        if (string.Equals(mode, "Clarification", StringComparison.OrdinalIgnoreCase))
        {
            return "Clarification";
        }

        if (string.Equals(mode, "Unknown", StringComparison.OrdinalIgnoreCase) || string.Equals(commandType, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        return "Command";
    }

    private AssistantCommand? Validate(AssistantCommand command, AiCommandContext context)
    {
        if (command.Mode == "Unknown" || command.CommandType == "unknown")
        {
            return Clarification("Команда не распознана. Можно спросить: «Покажи доступные команды».", []);
        }

        if (command.ProductId is not null && context.Products.All(x => x.Id != command.ProductId))
        {
            return Clarification("Товар из команды не найден в справочнике. Уточните название или SKU.", []);
        }

        if (command.SourceCellId is not null && context.Cells.All(x => x.Id != command.SourceCellId))
        {
            return Clarification("Исходная ячейка не найдена. Уточните код ячейки.", []);
        }

        if (command.TargetCellId is not null && context.Cells.All(x => x.Id != command.TargetCellId))
        {
            return Clarification("Целевая ячейка не найдена. Уточните код ячейки.", []);
        }

        var definition = commandRegistry.Find(command.CommandType);
        if (definition?.RequiresProduct == true && command.ProductId is null)
        {
            return Clarification(
                "Уточните товар: укажите название или SKU.",
                context.Products.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList());
        }

        if (command.CommandType == "create_product" && string.IsNullOrWhiteSpace(command.Name))
        {
            return Clarification("Уточните название товара. Например: «Создай товар Перчатки SKU GLV-01 мин 5».", []);
        }

        if (command.CommandType == "create_product" && string.IsNullOrWhiteSpace(command.Sku))
        {
            return Clarification("Уточните SKU товара. Например: «Создай товар Перчатки SKU GLV-01».", []);
        }

        if (command.CommandType == "create_product" && context.Products.Any(x => x.Sku.Equals(command.Sku, StringComparison.OrdinalIgnoreCase)))
        {
            return Clarification("Товар с таким SKU уже существует.", []);
        }

        if (command.CommandType == "update_min_stock" && command.MinQuantity is null or < 0)
        {
            return Clarification("Укажите новое значение минимального остатка не ниже нуля.", []);
        }

        if (definition?.RequiresQuantity == true && !HasPositiveQuantity(command))
        {
            return Clarification("Укажите положительное количество.", []);
        }

        if (definition?.RequiresSourceCell == true && command.SourceCellId is null)
        {
            return Clarification("Укажите исходную ячейку.", CellChoices(context));
        }

        if (definition?.RequiresTargetCell == true && command.TargetCellId is null)
        {
            return Clarification("Укажите целевую ячейку.", CellChoices(context));
        }

        if (definition?.RequiresSourceCell == true && definition.RequiresTargetCell && command.SourceCellId == command.TargetCellId)
        {
            return Clarification("Исходная и целевая ячейки должны отличаться.", CellChoices(context));
        }

        return null;
    }

    private static bool HasPositiveQuantity(AssistantCommand command) => command.Quantity is > 0;

    private static List<AssistantChoice> CellChoices(AiCommandContext context)
    {
        return context.Cells.Take(8).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Code} · {x.Name}", "cell")).ToList();
    }

    private static AssistantCommand Clarification(string question, List<AssistantChoice> choices)
    {
        return new AssistantCommand
        {
            Mode = "Clarification",
            Provider = "Server",
            CommandType = "unknown",
            RiskLevel = "None",
            RequiresConfirmation = false,
            Summary = question,
            ClarificationQuestion = question,
            Choices = choices
        };
    }
}
