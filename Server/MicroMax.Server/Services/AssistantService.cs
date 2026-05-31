using System.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MicroMax.Server.Services;

public sealed class AssistantService(HttpClient httpClient, IConfiguration configuration, MicroMaxDbContext db)
{
    private const string Model = "gpt-5.1";
    private static readonly ConcurrentDictionary<string, AssistantCommand> PendingCommands = new();

    public async Task<AssistantCommand> InterpretAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Команда не должна быть пустой.");
        }

        var apiKey = configuration["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Для командного помощника не задан OPENAI_API_KEY.");
        }

        var products = await db.Products.Select(x => new { x.Id, x.Name, x.Sku }).ToListAsync();
        var cells = await db.StorageCells.Select(x => new { x.Id, x.Code, x.Name }).ToListAsync();

        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                commandType = new { type = "string", @enum = new[] { "find_product", "stock_query", "cell_contents", "receive", "move", "write_off", "unknown" } },
                productId = new { type = new[] { "integer", "null" } },
                sourceCellId = new { type = new[] { "integer", "null" } },
                targetCellId = new { type = new[] { "integer", "null" } },
                quantity = new { type = new[] { "number", "null" } },
                requiresConfirmation = new { type = "boolean" },
                summary = new { type = "string" }
            },
            required = new[] { "commandType", "productId", "sourceCellId", "targetCellId", "quantity", "requiresConfirmation", "summary" }
        };

        var prompt = $"""
        Ты командный помощник системы MicroMax для микросклада.
        Нужно вернуть только структурированный результат.
        Write-операции receive, move и write_off всегда требуют подтверждения.

        Номенклатура: {JsonSerializer.Serialize(products)}
        Ячейки: {JsonSerializer.Serialize(cells)}
        Команда пользователя: {text}
        """;

        var requestBody = new
        {
            model = Model,
            input = prompt,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "micromax_assistant_command",
                    strict = true,
                    schema
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API вернул ошибку: {response.StatusCode}. {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var outputText = ExtractOutputText(doc.RootElement);
        var command = JsonSerializer.Deserialize<AssistantCommand>(
            outputText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("OpenAI вернул пустую команду.");

        command.CommandId = Guid.NewGuid().ToString("N");
        command.RequiresConfirmation = command.CommandType is "receive" or "move" or "write_off";
        if (command.RequiresConfirmation)
        {
            PendingCommands[command.CommandId] = command;
        }

        return command;
    }

    public static bool TryTakePendingCommand(string commandId, out AssistantCommand? command) =>
        PendingCommands.Remove(commandId, out command);

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? "{}";
        }

        if (root.TryGetProperty("output", out var output))
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content))
                {
                    continue;
                }

                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "{}";
                    }
                }
            }
        }

        throw new InvalidOperationException("Не удалось прочитать структурированный ответ OpenAI.");
    }
}
