using System.Text;
using System.Text.Json;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Assistant.Prompting;

/// <summary>
/// Формирует prompt для реальных ИИ-провайдеров на основе реестра команд и текущего складского контекста.
/// </summary>
public sealed class AiCommandPromptBuilder(AiCommandRegistry commandRegistry)
{
    public string Build(AiCommandContext context, string text)
    {
        var products = context.Products.Select(x => new { x.Id, x.Name, x.Sku }).ToList();
        var cells = context.Cells.Select(x => new { x.Id, x.Code, x.Name }).ToList();
        var commandTypes = commandRegistry.Commands.Select(x => x.Type).Append(AiCommandRegistry.Unknown).Distinct().ToList();
        var risks = commandRegistry.Commands
            .GroupBy(x => x.RiskLevel)
            .ToDictionary(x => x.Key, x => x.Select(command => command.Type).ToList());

        var sb = new StringBuilder();
        sb.AppendLine("Ты командный помощник MicroMax для микросклада.");
        sb.AppendLine("Верни только JSON без markdown, комментариев и пояснений.");
        sb.AppendLine("Если команда неоднозначна, не угадывай. Верни mode = Clarification и варианты choices.");
        sb.AppendLine("Не выполняй операции. Только распознай намерение пользователя.");
        sb.AppendLine("Команды изменения данных требуют подтверждения на сервере.");
        sb.AppendLine();
        sb.AppendLine("Доступные commandType:");
        sb.AppendLine(string.Join(", ", commandTypes) + ".");
        sb.AppendLine("riskLevel: None, Low, Medium, High, Critical.");
        sb.AppendLine($"Соответствие рисков: {JsonSerializer.Serialize(risks)}.");
        sb.AppendLine();
        sb.AppendLine("JSON schema shape:");
        sb.AppendLine("""
{
  "mode": "Command|Clarification|Unknown",
  "commandType": "string",
  "riskLevel": "None|Low|Medium|High|Critical",
  "productId": 0,
  "sourceCellId": 0,
  "targetCellId": 0,
  "quantity": 0,
  "minQuantity": 0,
  "sku": "string",
  "name": "string",
  "unit": "string",
  "summary": "string",
  "clarificationQuestion": "string",
  "choices": [{ "id": "string", "label": "string", "kind": "product|cell|command" }]
}
""");
        sb.AppendLine();
        sb.AppendLine("Правила:");
        sb.AppendLine("- Для поиска товара, перемещения, списания, поступления и изменения минимального остатка нужен productId.");
        sb.AppendLine("- Для списания нужна sourceCellId.");
        sb.AppendLine("- Для перемещения нужны sourceCellId, targetCellId, quantity.");
        sb.AppendLine("- Для поступления нужна targetCellId и quantity.");
        sb.AppendLine("- Если подходит несколько товаров или ячеек, верни Clarification с choices.");
        sb.AppendLine("- Если не хватает количества или ячейки, верни Clarification с вопросом.");
        sb.AppendLine("- Nullable поля можно не указывать или вернуть null.");
        sb.AppendLine();
        sb.AppendLine($"Описание команд: {JsonSerializer.Serialize(commandRegistry.Commands)}");
        sb.AppendLine($"Номенклатура: {JsonSerializer.Serialize(products)}");
        sb.AppendLine($"Ячейки: {JsonSerializer.Serialize(cells)}");
        sb.AppendLine($"Команда пользователя: {text}");

        return sb.ToString();
    }
}
