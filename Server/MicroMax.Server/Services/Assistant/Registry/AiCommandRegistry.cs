namespace MicroMax.Server.Services.Assistant.Registry;

/// <summary>
/// Единый реестр команд командного интерфейса. Prompt, mock-распознавание, валидация и API справки берут данные отсюда.
/// </summary>
public sealed class AiCommandRegistry
{
    public const string Unknown = "unknown";

    private readonly IReadOnlyList<AiCommandDefinition> _commands =
    [
        new("open_products", "Открыть список товаров", "Переход к экрану номенклатуры.", "None", false, false, false, false, false,
            ["Открой список товаров", "Покажи номенклатуру"],
            ["открой список товаров", "список товаров", "номенклатур"]),
        new("find_product", "Найти товар", "Поиск товара по названию или SKU.", "None", true, false, false, false, false,
            ["Найди перчатки", "Где лежит SKU GLV-001"],
            ["найди", "найти", "где леж", "покажи товар"]),
        new("low_stock", "Показать товары с низким остатком", "Позиции, у которых остаток не выше минимального.", "None", false, false, false, false, false,
            ["Покажи низкие остатки"],
            ["низк", "мало", "заканч"]),
        new("zero_stock", "Показать товары с нулевым остатком", "Позиции без положительного остатка по ячейкам.", "None", false, false, false, false, false,
            ["Покажи товары с нулевым остатком"],
            ["нулев", "нет остат"]),
        new("warehouse_summary", "Показать сводку по складу", "Краткая сводка по товарам, ячейкам и операциям.", "None", false, false, false, false, false,
            ["Покажи сводку по складу"],
            ["сводк", "итоги", "статист"]),
        new("create_product", "Создать товар", "Создание новой номенклатуры без начального остатка.", "Medium", false, false, false, false, true,
            ["Создай товар Перчатки SKU GLV-01 мин 5"],
            ["создай товар", "создать товар", "добавь товар", "добавить товар"]),
        new("update_min_stock", "Изменить минимальный остаток", "Изменение контрольного уровня остатка для товара.", "Medium", true, false, false, false, true,
            ["Измени минимальный остаток перчаток на 10"],
            ["минимальн", "минимум", "мин остат"]),
        new("move_product", "Переместить товар", "Перемещение остатка между ячейками.", "High", true, true, true, true, true,
            ["Перемести 3 GLV-001 из A-1 в A-2"],
            ["перемест", "перенеси"]),
        new("write_off_product", "Списать товар", "Списание остатка из выбранной ячейки.", "High", true, true, false, true, true,
            ["Спиши 2 GLV-001 из A-1"],
            ["спис", "спиш", "расход"]),
        new("create_receipt", "Создать поступление", "Подготовка черновика поступления.", "Low", true, false, false, false, false,
            ["Создай поступление GLV-001 10 штук"],
            ["создай поступ", "создать поступ", "черновик поступ"]),
        new("post_receipt", "Провести поступление", "Увеличение остатка в целевой ячейке.", "High", true, false, true, true, true,
            ["Проведи поступление 10 GLV-001 в A-1"],
            ["проведи поступ", "провести поступ", "прими", "приём", "прием"]),
        new("cancel", "Отменить команду", "Отмена ожидающей подтверждения команды.", "None", false, false, false, false, false,
            ["Отмени команду"],
            ["отмени", "отменить", "стоп"]),
        new("help", "Показать доступные команды", "Справка по возможностям помощника.", "None", false, false, false, false, false,
            ["Что ты умеешь?"],
            ["что ты умеешь", "доступные команды", "помощь", "help"])
    ];

    public IReadOnlyList<AiCommandDefinition> Commands => _commands;

    public AiCommandDefinition? Find(string? type)
    {
        return _commands.FirstOrDefault(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    public string NormalizeType(string? type)
    {
        var value = type?.Trim().ToLowerInvariant().Replace("-", "_") ?? Unknown;
        value = value switch
        {
            "receive" => "post_receipt",
            "move" => "move_product",
            "write_off" => "write_off_product",
            _ => value
        };

        return Find(value)?.Type ?? Unknown;
    }

    public AiCommandDefinition UnknownDefinition =>
        new(Unknown, "Неизвестная команда", "Команда не распознана.", "None", false, false, false, false, false, [], []);
}
