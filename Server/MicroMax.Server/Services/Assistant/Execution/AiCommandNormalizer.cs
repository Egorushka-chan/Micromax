using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Assistant.Execution;

/// <summary>
/// Приводит ответ провайдера к серверному контракту и отсеивает неполные
/// или небезопасные команды до отправки на клиент.
/// </summary>
public sealed class AiCommandNormalizer(AiCommandRegistry commandRegistry, AiCommandRules commandRules)
{
    public AssistantCommand Normalize(AssistantCommand command, AiCommandContext context, AiProviderKind provider)
    {
        command.CommandId = string.IsNullOrWhiteSpace(command.CommandId) ? Guid.NewGuid().ToString("N") : command.CommandId;
        command.Provider = provider.ToString();
        command.Mode = NormalizeMode(command.Mode, command.CommandType);
        command.CommandType = commandRegistry.NormalizeType(command.CommandType);
        command.ClarificationTarget = NormalizeClarificationTarget(command.ClarificationTarget, command.Choices);
        command.RiskLevel = commandRules.RiskFor(command.CommandType);
        command.RequiresConfirmation = command.Mode == "Command" && command.RiskLevel is "Medium" or "High" or "Critical";

        if (command.Mode == "Clarification")
        {
            command.RiskLevel = "None";
            command.RequiresConfirmation = false;
            command.Summary = command.ClarificationQuestion ?? command.Summary;
            return command;
        }

        var validation = Validate(command, context);
        if (validation is not null)
        {
            validation.Provider = "Server";
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

        if (string.Equals(mode, "Unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandType, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return "Unknown";
        }

        return "Command";
    }

    private AssistantCommand? Validate(AssistantCommand command, AiCommandContext context)
    {
        if (command.Mode == "Unknown" || command.CommandType == "unknown")
        {
            return Clarification(
                command,
                "Команда не распознана. Можно спросить: «Покажи доступные команды».",
                [],
                "Command");
        }

        if (command.ProductId is not null && context.Products.All(x => x.Id != command.ProductId))
        {
            return Clarification(
                command,
                "Товар из команды не найден в справочнике. Уточните название или SKU.",
                context.Products.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList(),
                "Product");
        }

        if (command.SourceCellId is not null && context.Cells.All(x => x.Id != command.SourceCellId))
        {
            return Clarification(
                command,
                "Исходная ячейка не найдена. Уточните код ячейки.",
                CellChoices(context),
                "SourceCell");
        }

        if (command.TargetCellId is not null && context.Cells.All(x => x.Id != command.TargetCellId))
        {
            return Clarification(
                command,
                "Целевая ячейка не найдена. Уточните код ячейки.",
                CellChoices(context),
                "TargetCell");
        }

        var definition = commandRegistry.Find(command.CommandType);
        if (definition?.RequiresProduct == true && command.ProductId is null)
        {
            return Clarification(
                command,
                "Уточните товар: укажите название или SKU.",
                context.Products.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList(),
                "Product");
        }

        if (command.CommandType == "create_product" && string.IsNullOrWhiteSpace(command.Name))
        {
            return Clarification(
                command,
                "Уточните название товара. Например: «Создай товар Перчатки SKU GLV-01 мин 5».",
                [],
                null);
        }

        if (command.CommandType == "create_product" && string.IsNullOrWhiteSpace(command.Sku))
        {
            return Clarification(
                command,
                "Уточните SKU товара. Например: «Создай товар Перчатки SKU GLV-01».",
                [],
                null);
        }

        if (command.CommandType == "create_product" &&
            context.Products.Any(x => x.Sku.Equals(command.Sku, StringComparison.OrdinalIgnoreCase)))
        {
            return Clarification(
                command,
                "Товар с таким SKU уже существует.",
                [],
                null);
        }

        if (command.CommandType == "update_min_stock" && command.MinQuantity is null or < 0)
        {
            return Clarification(
                command,
                "Укажите новое значение минимального остатка не ниже нуля.",
                [],
                null);
        }

        if (definition?.RequiresQuantity == true && !HasPositiveQuantity(command))
        {
            return Clarification(
                command,
                "Укажите положительное количество.",
                [],
                null);
        }

        if (definition?.RequiresSourceCell == true && command.SourceCellId is null)
        {
            return Clarification(
                command,
                "Укажите исходную ячейку.",
                CellChoices(context),
                "SourceCell");
        }

        if (definition?.RequiresTargetCell == true && command.TargetCellId is null)
        {
            return Clarification(
                command,
                "Укажите целевую ячейку.",
                CellChoices(context),
                "TargetCell");
        }

        if (definition?.RequiresSourceCell == true &&
            definition.RequiresTargetCell &&
            command.SourceCellId == command.TargetCellId)
        {
            return Clarification(
                command,
                "Исходная и целевая ячейки должны отличаться.",
                CellChoices(context),
                "TargetCell");
        }

        return null;
    }

    private static bool HasPositiveQuantity(AssistantCommand command) => command.Quantity is > 0;

    private static List<AssistantChoice> CellChoices(AiCommandContext context)
    {
        return context.Cells
            .Take(8)
            .Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Code} · {x.Name}", "cell"))
            .ToList();
    }

    private static string? NormalizeClarificationTarget(string? target, IReadOnlyList<AssistantChoice> choices)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target.Trim() switch
            {
                "product" or "Product" => "Product",
                "sourcecell" or "SourceCell" => "SourceCell",
                "targetcell" or "TargetCell" => "TargetCell",
                "command" or "Command" => "Command",
                _ => target.Trim()
            };
        }

        var kinds = choices
            .Select(choice => choice.Kind.Trim().ToLowerInvariant())
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct()
            .ToList();

        return kinds.Count == 1
            ? kinds[0] switch
            {
                "product" => "Product",
                "command" => "Command",
                _ => null
            }
            : null;
    }

    private static AssistantCommand Clarification(
        AssistantCommand command,
        string question,
        List<AssistantChoice> choices,
        string? target)
    {
        command.Mode = "Clarification";
        command.RiskLevel = "None";
        command.RequiresConfirmation = false;
        command.Summary = question;
        command.ClarificationQuestion = question;
        command.ClarificationTarget = target;
        command.Choices = choices;
        return command;
    }
}
