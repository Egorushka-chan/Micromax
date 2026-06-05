using System.Text;
using System.Text.Json;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Registry;

namespace MicroMax.Server.Services.Assistant.Prompting;

/// <summary>
/// Builds a strict prompt for real AI providers from the command registry and
/// the current warehouse context.
/// </summary>
public sealed class AiCommandPromptBuilder(AiCommandRegistry commandRegistry)
{
    public string Build(AiCommandContext context, string text)
    {
        var products = context.Products.Select(x => new { x.Id, x.Name, x.Sku }).ToList();
        var cells = context.Cells.Select(x => new { x.Id, x.Code, x.Name }).ToList();
        var commandTypes = commandRegistry.Commands
            .Select(x => x.Type)
            .Append(AiCommandRegistry.Unknown)
            .Distinct()
            .ToList();
        var risks = commandRegistry.Commands
            .GroupBy(x => x.RiskLevel)
            .ToDictionary(x => x.Key, x => x.Select(command => command.Type).ToList());

        var sb = new StringBuilder();
        sb.AppendLine("You are the MicroMax warehouse command interpreter.");
        sb.AppendLine("Return JSON only. No markdown, no explanations, no comments.");
        sb.AppendLine("Do not execute anything. Only interpret the user's intent.");
        sb.AppendLine("If the intent is ambiguous or some required field is missing, return mode = Clarification.");
        sb.AppendLine("When clarification is needed, preserve the intended commandType and already known fields.");
        sb.AppendLine("Commands that change data will be confirmed later on the server.");
        sb.AppendLine();
        sb.AppendLine("Allowed commandType values:");
        sb.AppendLine(string.Join(", ", commandTypes) + ".");
        sb.AppendLine("Allowed riskLevel values: None, Low, Medium, High, Critical.");
        sb.AppendLine($"Known risks by command: {JsonSerializer.Serialize(risks)}.");
        sb.AppendLine();
        sb.AppendLine("JSON schema:");
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
  "clarificationTarget": "Product|SourceCell|TargetCell|Command",
  "choices": [{ "id": "string", "label": "string", "kind": "product|cell|command" }]
}
""");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Use commandType only from the allowed list.");
        sb.AppendLine("- For find_product, move_product, write_off_product, post_receipt, create_receipt and update_min_stock, identify the product when possible.");
        sb.AppendLine("- For move_product you need sourceCellId, targetCellId and quantity.");
        sb.AppendLine("- For write_off_product you need sourceCellId and quantity.");
        sb.AppendLine("- For post_receipt you need targetCellId and quantity.");
        sb.AppendLine("- For create_product identify name, sku, unit and minQuantity when mentioned.");
        sb.AppendLine("- If several products or cells match, return Clarification with choices.");
        sb.AppendLine("- If the command itself is unclear, use clarificationTarget = Command.");
        sb.AppendLine("- Omit unknown optional fields or return null-like empty values.");
        sb.AppendLine();
        sb.AppendLine($"Command registry: {JsonSerializer.Serialize(commandRegistry.Commands)}");
        sb.AppendLine($"Products: {JsonSerializer.Serialize(products)}");
        sb.AppendLine($"Cells: {JsonSerializer.Serialize(cells)}");
        sb.AppendLine($"User text: {text}");

        return sb.ToString();
    }
}
