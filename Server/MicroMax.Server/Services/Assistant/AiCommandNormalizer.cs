using MicroMax.Server.Models;

namespace MicroMax.Server.Services.Assistant;

public sealed class AiCommandNormalizer
{
    public AssistantCommand Normalize(AssistantCommand command, AiCommandContext context, AiProviderKind provider)
    {
        command.CommandId = string.IsNullOrWhiteSpace(command.CommandId) ? Guid.NewGuid().ToString("N") : command.CommandId;
        command.Provider = provider.ToString();
        command.Mode = NormalizeMode(command.Mode, command.CommandType);
        command.CommandType = NormalizeCommandType(command.CommandType);
        command.RiskLevel = AiCommandRules.RiskFor(command.CommandType);
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
            command.Summary = AiCommandRules.BuildSummary(command, context);
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

    private static string NormalizeCommandType(string? commandType)
    {
        var value = commandType?.Trim().ToLowerInvariant().Replace("-", "_") ?? "unknown";
        return value switch
        {
            "receive" => "post_receipt",
            "move" => "move_product",
            "write_off" => "write_off_product",
            _ => value
        };
    }

    private static AssistantCommand? Validate(AssistantCommand command, AiCommandContext context)
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

        if (AiCommandRules.RequiresProduct(command.CommandType) && command.ProductId is null)
        {
            return Clarification(
                "Уточните товар: укажите название или SKU.",
                context.Products.Take(6).Select(x => new AssistantChoice(x.Id.ToString(), $"{x.Name} · {x.Sku}", "product")).ToList());
        }

        return command.CommandType switch
        {
            "create_product" when string.IsNullOrWhiteSpace(command.Name) =>
                Clarification("Уточните название товара. Например: «Создай товар Перчатки SKU GLV-01 мин 5».", []),
            "create_product" when string.IsNullOrWhiteSpace(command.Sku) =>
                Clarification("Уточните SKU товара. Например: «Создай товар Перчатки SKU GLV-01».", []),
            "create_product" when context.Products.Any(x => x.Sku.Equals(command.Sku, StringComparison.OrdinalIgnoreCase)) =>
                Clarification("Товар с таким SKU уже существует.", []),
            "update_min_stock" when command.MinQuantity is null or < 0 =>
                Clarification("Укажите новое значение минимального остатка не ниже нуля.", []),
            "move_product" when !HasPositiveQuantity(command) =>
                Clarification("Укажите положительное количество для перемещения.", []),
            "move_product" when command.SourceCellId is null || command.TargetCellId is null =>
                Clarification("Укажите исходную и целевую ячейки.", CellChoices(context)),
            "move_product" when command.SourceCellId == command.TargetCellId =>
                Clarification("Исходная и целевая ячейки должны отличаться.", CellChoices(context)),
            "write_off_product" when !HasPositiveQuantity(command) =>
                Clarification("Укажите положительное количество для списания.", []),
            "write_off_product" when command.SourceCellId is null =>
                Clarification("Укажите исходную ячейку.", CellChoices(context)),
            "post_receipt" when !HasPositiveQuantity(command) =>
                Clarification("Укажите положительное количество для поступления.", []),
            "post_receipt" when command.TargetCellId is null =>
                Clarification("Укажите целевую ячейку для поступления.", CellChoices(context)),
            _ => null
        };
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
