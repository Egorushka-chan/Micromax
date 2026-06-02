using System.Text;
using System.Text.Json;

namespace MicroMax.Server.Services.Assistant;

public sealed class AiCommandPromptBuilder
{
    public string Build(AiCommandContext context, string text)
    {
        var products = context.Products.Select(x => new { x.Id, x.Name, x.Sku }).ToList();
        var cells = context.Cells.Select(x => new { x.Id, x.Code, x.Name }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Ты командный помощник MicroMax для микросклада.");
        sb.AppendLine("Верни только JSON без markdown, комментариев и пояснений.");
        sb.AppendLine("Если команда неоднозначна, не угадывай. Верни mode = Clarification и варианты choices.");
        sb.AppendLine("Не выполняй операции. Только распознай намерение пользователя.");
        sb.AppendLine("Команды изменения данных требуют подтверждения на сервере.");
        sb.AppendLine();
        sb.AppendLine("Доступные commandType:");
        sb.AppendLine("open_products, find_product, low_stock, zero_stock, warehouse_summary, create_product, update_min_stock, move_product, write_off_product, create_receipt, post_receipt, cancel, help, unknown.");
        sb.AppendLine("riskLevel: None, Low, Medium, High, Critical.");
        sb.AppendLine("create_product/update_min_stock = Medium. move_product/write_off_product/post_receipt = High. create_receipt = Low. Остальные = None.");
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
        sb.AppendLine($"Номенклатура: {JsonSerializer.Serialize(products)}");
        sb.AppendLine($"Ячейки: {JsonSerializer.Serialize(cells)}");
        sb.AppendLine($"Команда пользователя: {text}");

        return sb.ToString();
    }
}
