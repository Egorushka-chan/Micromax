package com.bezborodov.micromax.domain.assistant

import com.bezborodov.micromax.data.CreateProductRequest
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.WarehouseSnapshot
import java.util.Locale
import java.util.UUID

enum class AiCommandType {
    OpenProductList,
    FindProduct,
    ShowLowStockProducts,
    ShowZeroStockProducts,
    ShowWarehouseSummary,
    CreateProduct,
    UpdateMinStock,
    MoveProduct,
    WriteOffProduct,
    CreateReceipt,
    PostReceipt,
    CancelCommand,
    ShowAvailableCommands,
    Unknown
}

enum class AiCommandRiskLevel {
    None,
    Low,
    Medium,
    High,
    Critical
}

data class AiCommandDefinition(
    val type: AiCommandType,
    val title: String,
    val description: String,
    val riskLevel: AiCommandRiskLevel,
    val examples: List<String>
)

data class AiCommand(
    val id: String = UUID.randomUUID().toString(),
    val type: AiCommandType,
    val originalText: String,
    val riskLevel: AiCommandRiskLevel = AiCommandRegistry.definitionFor(type).riskLevel,
    val productId: Int? = null,
    val productQuery: String? = null,
    val sourceCellId: Int? = null,
    val targetCellId: Int? = null,
    val quantity: Double? = null,
    val minQuantity: Double? = null,
    val sku: String? = null,
    val name: String? = null,
    val unit: String? = null,
    val summary: String = AiCommandRegistry.definitionFor(type).title
) {
    val requiresConfirmation: Boolean
        get() = riskLevel.ordinal >= AiCommandRiskLevel.Medium.ordinal
}

data class AiCommandResult(
    val success: Boolean,
    val message: String,
    val details: List<String> = emptyList(),
    val navigationTarget: AiNavigationTarget? = null
)

enum class AiNavigationTarget {
    Products,
    Operations
}

object AiCommandRegistry {
    val definitions: List<AiCommandDefinition> = listOf(
        AiCommandDefinition(
            type = AiCommandType.OpenProductList,
            title = "Открыть список товаров",
            description = "Переход к экрану номенклатуры.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Открой список товаров", "Покажи номенклатуру")
        ),
        AiCommandDefinition(
            type = AiCommandType.FindProduct,
            title = "Найти товар",
            description = "Поиск товара по названию или SKU.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Найди перчатки", "Где лежит SKU GLOVES")
        ),
        AiCommandDefinition(
            type = AiCommandType.ShowLowStockProducts,
            title = "Показать товары с низким остатком",
            description = "Выводит позиции, у которых остаток не выше минимального.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Покажи низкие остатки")
        ),
        AiCommandDefinition(
            type = AiCommandType.ShowZeroStockProducts,
            title = "Показать товары с нулевым остатком",
            description = "Выводит позиции без остатка по ячейкам.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Покажи товары с нулевым остатком")
        ),
        AiCommandDefinition(
            type = AiCommandType.ShowWarehouseSummary,
            title = "Показать сводку по складу",
            description = "Краткая сводка по товарам, ячейкам и операциям.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Покажи сводку по складу")
        ),
        AiCommandDefinition(
            type = AiCommandType.CreateProduct,
            title = "Создать товар",
            description = "Создание новой номенклатуры без начального остатка.",
            riskLevel = AiCommandRiskLevel.Medium,
            examples = listOf("Создай товар Перчатки SKU GLV-01 мин 5")
        ),
        AiCommandDefinition(
            type = AiCommandType.UpdateMinStock,
            title = "Изменить минимальный остаток",
            description = "Изменение контрольного уровня остатка для товара.",
            riskLevel = AiCommandRiskLevel.Medium,
            examples = listOf("Измени минимальный остаток перчаток на 10")
        ),
        AiCommandDefinition(
            type = AiCommandType.MoveProduct,
            title = "Переместить товар",
            description = "Перемещение остатка между ячейками.",
            riskLevel = AiCommandRiskLevel.High,
            examples = listOf("Перемести 3 перчатки из A-01 в B-02")
        ),
        AiCommandDefinition(
            type = AiCommandType.WriteOffProduct,
            title = "Списать товар",
            description = "Списание остатка из выбранной ячейки.",
            riskLevel = AiCommandRiskLevel.High,
            examples = listOf("Спиши 2 перчатки из A-01")
        ),
        AiCommandDefinition(
            type = AiCommandType.CreateReceipt,
            title = "Создать поступление",
            description = "Подготовка черновика поступления в рамках MVP.",
            riskLevel = AiCommandRiskLevel.Low,
            examples = listOf("Создай поступление перчаток 10 штук")
        ),
        AiCommandDefinition(
            type = AiCommandType.PostReceipt,
            title = "Провести поступление",
            description = "Увеличение остатка в целевой ячейке.",
            riskLevel = AiCommandRiskLevel.High,
            examples = listOf("Проведи поступление 10 перчаток в A-01")
        ),
        AiCommandDefinition(
            type = AiCommandType.CancelCommand,
            title = "Отменить команду",
            description = "Отмена ожидающей подтверждения команды.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Отмени команду")
        ),
        AiCommandDefinition(
            type = AiCommandType.ShowAvailableCommands,
            title = "Показать доступные команды",
            description = "Справка по возможностям помощника.",
            riskLevel = AiCommandRiskLevel.None,
            examples = listOf("Что ты умеешь?")
        )
    )

    fun definitionFor(type: AiCommandType): AiCommandDefinition {
        return definitions.firstOrNull { it.type == type }
            ?: AiCommandDefinition(type, "Неизвестная команда", "Команда не распознана.", AiCommandRiskLevel.None, emptyList())
    }
}

interface AiCommandParser {
    fun parse(text: String, snapshot: WarehouseSnapshot): AiCommand
}

class RuleBasedAiCommandParser : AiCommandParser {
    override fun parse(text: String, snapshot: WarehouseSnapshot): AiCommand {
        val normalized = text.trim()
        val lower = normalized.lowercase(Locale.getDefault())
        if (lower.isBlank()) {
            return AiCommand(type = AiCommandType.Unknown, originalText = text, summary = "Введите команду для помощника.")
        }

        val type = when {
            lower.hasAny("отмени", "отменить", "стоп") -> AiCommandType.CancelCommand
            lower.hasAny("что ты умеешь", "доступные команды", "помощь", "help") -> AiCommandType.ShowAvailableCommands
            lower.hasAny("спис", "расход") -> AiCommandType.WriteOffProduct
            lower.hasAny("перемест", "перенеси") -> AiCommandType.MoveProduct
            lower.hasAny("проведи поступ", "провести поступ", "прими", "приём", "прием") -> AiCommandType.PostReceipt
            lower.hasAny("создай поступ", "создать поступ", "черновик поступ") -> AiCommandType.CreateReceipt
            lower.hasAny("создай товар", "создать товар", "добавь товар", "добавить товар") -> AiCommandType.CreateProduct
            lower.hasAny("минимальн", "минимум", "мин остат") -> AiCommandType.UpdateMinStock
            lower.hasAny("нулев", "нет остат") -> AiCommandType.ShowZeroStockProducts
            lower.hasAny("низк", "мало", "заканч") -> AiCommandType.ShowLowStockProducts
            lower.hasAny("сводк", "итоги", "статист") -> AiCommandType.ShowWarehouseSummary
            lower.hasAny("открой список товаров", "список товаров", "номенклатур") -> AiCommandType.OpenProductList
            lower.hasAny("найди", "найти", "где леж", "покажи товар") -> AiCommandType.FindProduct
            else -> AiCommandType.Unknown
        }

        val product = findSingleProduct(lower, snapshot)
        val cells = snapshot.cells.filter { lower.contains(it.code.lowercase(Locale.getDefault())) }
            .sortedBy { lower.indexOf(it.code.lowercase(Locale.getDefault())) }
        val textWithoutCells = cells.fold(lower) { current, cell ->
            current.replace(cell.code.lowercase(Locale.getDefault()), " ")
        }
        val quantity = NUMBER_REGEX.find(textWithoutCells)?.groupValues?.getOrNull(1)?.parseNumber()
        val minQuantity = MIN_REGEX.find(lower)?.groupValues?.getOrNull(1)?.parseNumber() ?: quantity
        val sku = SKU_REGEX.find(normalized)?.groupValues?.getOrNull(1)
        val createName = CREATE_NAME_REGEX.find(normalized)?.groupValues?.getOrNull(1)?.trim()

        return AiCommand(
            type = type,
            originalText = text,
            productId = product?.id,
            productQuery = product?.name ?: extractProductQuery(normalized, type),
            sourceCellId = cells.firstOrNull()?.id,
            targetCellId = when (type) {
                AiCommandType.MoveProduct -> cells.drop(1).firstOrNull()?.id
                AiCommandType.PostReceipt -> cells.firstOrNull()?.id
                else -> null
            },
            quantity = quantity,
            minQuantity = minQuantity,
            sku = sku,
            name = createName,
            unit = if (lower.contains("кг")) "кг" else "шт",
            summary = AiCommandRegistry.definitionFor(type).title
        )
    }

    private fun findSingleProduct(lower: String, snapshot: WarehouseSnapshot) =
        snapshot.products.firstOrNull { product ->
            lower.contains(product.sku.lowercase(Locale.getDefault())) ||
                lower.contains(product.name.lowercase(Locale.getDefault()))
        } ?: snapshot.products.filter { product ->
            lower.meaningfulTokens().any { token ->
                product.name.matchesProductToken(token) ||
                    product.sku.matchesProductToken(token)
            }
        }.singleOrNull()

    private fun extractProductQuery(text: String, type: AiCommandType): String? {
        val cleaned = text
            .replace(Regex("(?i)найди|найти|где лежит|где лежат|покажи товар|товар"), "")
            .trim()
        return when {
            type == AiCommandType.CreateProduct -> CREATE_NAME_REGEX.find(text)?.groupValues?.getOrNull(1)?.trim()
            cleaned.isBlank() -> null
            else -> cleaned
        }
    }
}

class AiCommandValidator {
    fun validate(command: AiCommand, snapshot: WarehouseSnapshot): AiCommandResult? {
        val productMatches = command.productQuery?.let { query ->
            val tokens = query.lowercase(Locale.getDefault()).meaningfulTokens()
            snapshot.products.filter {
                it.name.contains(query, ignoreCase = true) ||
                    it.sku.contains(query, ignoreCase = true) ||
                    tokens.any { token ->
                        it.name.matchesProductToken(token) ||
                            it.sku.matchesProductToken(token)
                    }
            }
        }.orEmpty()

        if (command.type in setOf(
                AiCommandType.FindProduct,
                AiCommandType.UpdateMinStock,
                AiCommandType.MoveProduct,
                AiCommandType.WriteOffProduct,
                AiCommandType.CreateReceipt,
                AiCommandType.PostReceipt
            ) && command.productId == null
        ) {
            return when {
                productMatches.size > 1 -> AiCommandResult(
                    success = false,
                    message = "Найдено несколько товаров. Уточните команду по SKU или полному названию.",
                    details = productMatches.take(5).map { "${it.name} · ${it.sku}" }
                )
                else -> AiCommandResult(false, "Не удалось определить товар. Укажите название или SKU.")
            }
        }

        return when (command.type) {
            AiCommandType.CreateProduct -> when {
                command.name.isNullOrBlank() -> AiCommandResult(false, "Уточните название товара. Например: «Создай товар Перчатки SKU GLV-01 мин 5».")
                command.sku.isNullOrBlank() -> AiCommandResult(false, "Уточните SKU товара. Например: «Создай товар Перчатки SKU GLV-01».")
                snapshot.products.any { it.sku.equals(command.sku, ignoreCase = true) } -> AiCommandResult(false, "Товар с таким SKU уже существует.")
                else -> null
            }
            AiCommandType.UpdateMinStock -> if (command.minQuantity == null || command.minQuantity < 0.0) {
                AiCommandResult(false, "Укажите новое значение минимального остатка не ниже нуля.")
            } else null
            AiCommandType.MoveProduct -> validateQuantityAndCells(command, needSource = true, needTarget = true)
            AiCommandType.WriteOffProduct -> validateQuantityAndCells(command, needSource = true, needTarget = false)
            AiCommandType.PostReceipt -> validateQuantityAndCells(command, needSource = false, needTarget = true)
            AiCommandType.Unknown -> AiCommandResult(false, "Команда не распознана. Можно спросить: «Покажи доступные команды».")
            else -> null
        }
    }

    private fun validateQuantityAndCells(command: AiCommand, needSource: Boolean, needTarget: Boolean): AiCommandResult? {
        return when {
            command.quantity == null || command.quantity <= 0.0 -> AiCommandResult(false, "Укажите положительное количество.")
            needSource && command.sourceCellId == null -> AiCommandResult(false, "Укажите исходную ячейку.")
            needTarget && command.targetCellId == null -> AiCommandResult(false, "Укажите целевую ячейку.")
            command.sourceCellId != null && command.sourceCellId == command.targetCellId -> AiCommandResult(false, "Исходная и целевая ячейки должны отличаться.")
            else -> null
        }
    }
}

interface AiCommandExecutor {
    suspend fun execute(command: AiCommand, snapshot: WarehouseSnapshot): AiCommandResult
}

class MockAiCommandExecutor(
    private val apiClient: MicroMaxApiClient
) : AiCommandExecutor {
    override suspend fun execute(command: AiCommand, snapshot: WarehouseSnapshot): AiCommandResult {
        return when (command.type) {
            AiCommandType.OpenProductList -> AiCommandResult(true, "Открываю список товаров.", navigationTarget = AiNavigationTarget.Products)
            AiCommandType.FindProduct -> findProduct(command, snapshot)
            AiCommandType.ShowLowStockProducts -> lowStock(snapshot)
            AiCommandType.ShowZeroStockProducts -> zeroStock(snapshot)
            AiCommandType.ShowWarehouseSummary -> warehouseSummary(snapshot)
            AiCommandType.CreateProduct -> createProduct(command)
            AiCommandType.UpdateMinStock -> updateMinStock(command, snapshot)
            AiCommandType.MoveProduct -> moveProduct(command)
            AiCommandType.WriteOffProduct -> writeOffProduct(command)
            AiCommandType.CreateReceipt -> AiCommandResult(
                success = true,
                message = "Черновик поступления подготовлен. В MVP он не сохраняется как отдельный документ.",
                navigationTarget = AiNavigationTarget.Operations
            )
            AiCommandType.PostReceipt -> postReceipt(command)
            AiCommandType.CancelCommand -> AiCommandResult(true, "Ожидающая команда отменена.")
            AiCommandType.ShowAvailableCommands -> AiCommandResult(
                success = true,
                message = "Доступные команды помощника:",
                details = AiCommandRegistry.definitions.map { "${it.title}: ${it.examples.firstOrNull().orEmpty()}" }
            )
            AiCommandType.Unknown -> AiCommandResult(false, "Команда не распознана. Спросите: «Покажи доступные команды».")
        }
    }

    private fun findProduct(command: AiCommand, snapshot: WarehouseSnapshot): AiCommandResult {
        val product = snapshot.products.firstOrNull { it.id == command.productId }
            ?: return AiCommandResult(false, "Товар не найден.")
        val locations = snapshot.stocks
            .filter { it.sku == product.sku && it.quantity > 0.0 }
            .map { "${it.zoneCode} / ${it.cellCode}: ${it.quantity} ${it.unit}" }
        return AiCommandResult(
            success = true,
            message = "Товар найден: ${product.name} · ${product.sku}.",
            details = if (locations.isEmpty()) listOf("Остатка по ячейкам нет.") else locations,
            navigationTarget = AiNavigationTarget.Products
        )
    }

    private fun lowStock(snapshot: WarehouseSnapshot): AiCommandResult {
        val rows = snapshot.products.map { product ->
            val total = snapshot.stocks.filter { it.sku == product.sku }.sumOf { it.quantity }
            product to total
        }.filter { (product, total) -> total > 0.0 && total <= product.minQuantity }

        return AiCommandResult(
            success = true,
            message = if (rows.isEmpty()) "Товаров с низким остатком нет." else "Товары с низким остатком:",
            details = rows.map { (product, total) -> "${product.name}: ${total} ${product.unit}, минимум ${product.minQuantity}" },
            navigationTarget = AiNavigationTarget.Products
        )
    }

    private fun zeroStock(snapshot: WarehouseSnapshot): AiCommandResult {
        val rows = snapshot.products.filter { product ->
            snapshot.stocks.none { it.sku == product.sku && it.quantity > 0.0 }
        }
        return AiCommandResult(
            success = true,
            message = if (rows.isEmpty()) "Товаров с нулевым остатком нет." else "Товары с нулевым остатком:",
            details = rows.map { "${it.name} · ${it.sku}" },
            navigationTarget = AiNavigationTarget.Products
        )
    }

    private fun warehouseSummary(snapshot: WarehouseSnapshot): AiCommandResult {
        val totalQuantity = snapshot.stocks.sumOf { it.quantity }
        return AiCommandResult(
            success = true,
            message = "Сводка по микроскладу сформирована.",
            details = listOf(
                "Номенклатура: ${snapshot.products.size}",
                "Ячейки хранения: ${snapshot.cells.size}",
                "Положительный остаток: $totalQuantity",
                "Операции в журнале: ${snapshot.operations.size}"
            )
        )
    }

    private fun createProduct(command: AiCommand): AiCommandResult {
        apiClient.createProduct(
            CreateProductRequest(
                sku = requireNotNull(command.sku).trim(),
                name = requireNotNull(command.name).trim(),
                unit = command.unit?.trim().takeUnless { it.isNullOrBlank() } ?: "шт",
                minQuantity = command.minQuantity ?: 0.0
            )
        )
        return AiCommandResult(true, "Товар создан: ${command.name}.", navigationTarget = AiNavigationTarget.Products)
    }

    private fun updateMinStock(command: AiCommand, snapshot: WarehouseSnapshot): AiCommandResult {
        val product = requireNotNull(snapshot.products.firstOrNull { it.id == command.productId })
        apiClient.updateProductMinQuantity(product, requireNotNull(command.minQuantity))
        return AiCommandResult(true, "Минимальный остаток обновлён для товара «${product.name}».", navigationTarget = AiNavigationTarget.Products)
    }

    private fun moveProduct(command: AiCommand): AiCommandResult {
        apiClient.move(
            productId = requireNotNull(command.productId),
            sourceCellId = requireNotNull(command.sourceCellId),
            targetCellId = requireNotNull(command.targetCellId),
            quantity = requireNotNull(command.quantity)
        )
        return AiCommandResult(true, "Перемещение выполнено.", navigationTarget = AiNavigationTarget.Operations)
    }

    private fun writeOffProduct(command: AiCommand): AiCommandResult {
        apiClient.writeOff(
            productId = requireNotNull(command.productId),
            sourceCellId = requireNotNull(command.sourceCellId),
            quantity = requireNotNull(command.quantity)
        )
        return AiCommandResult(true, "Списание выполнено.", navigationTarget = AiNavigationTarget.Operations)
    }

    private fun postReceipt(command: AiCommand): AiCommandResult {
        apiClient.receive(
            productId = requireNotNull(command.productId),
            targetCellId = requireNotNull(command.targetCellId),
            quantity = requireNotNull(command.quantity)
        )
        return AiCommandResult(true, "Поступление проведено.", navigationTarget = AiNavigationTarget.Operations)
    }
}

private val NUMBER_REGEX = Regex("""(\d+(?:[,.]\d+)?)""")
private val MIN_REGEX = Regex("""(?:мин(?:имальный)?\s*(?:остаток)?|минимум)\D*(\d+(?:[,.]\d+)?)""", RegexOption.IGNORE_CASE)
private val SKU_REGEX = Regex("""(?:sku|артикул)\s*[:\-]?\s*([A-Za-zА-Яа-я0-9_-]+)""", RegexOption.IGNORE_CASE)
private val CREATE_NAME_REGEX = Regex("""(?:создай|создать|добавь|добавить)\s+товар\s+(.+?)(?:\s+(?:sku|артикул|мин|минимальный|минимум)\b|$)""", RegexOption.IGNORE_CASE)

private fun String.hasAny(vararg tokens: String): Boolean = tokens.any { contains(it) }

private fun String.parseNumber(): Double? = replace(',', '.').toDoubleOrNull()

private fun String.matchesProductToken(token: String): Boolean {
    val lower = lowercase(Locale.getDefault())
    if (lower.contains(token)) {
        return true
    }
    return lower.meaningfulTokens().any { part ->
        part.length >= 4 && token.length >= 4 && part.take(4) == token.take(4)
    }
}

private fun String.meaningfulTokens(): List<String> {
    return split(Regex("""[^A-Za-zА-Яа-я0-9_-]+"""))
        .map { it.trim().lowercase(Locale.getDefault()) }
        .filter { it.length >= 3 && it !in STOP_WORDS && it.toDoubleOrNull() == null }
}

private val STOP_WORDS = setOf(
    "найди",
    "найти",
    "где",
    "лежит",
    "лежат",
    "покажи",
    "товар",
    "товары",
    "создай",
    "создать",
    "добавь",
    "добавить",
    "перемести",
    "переместить",
    "перенеси",
    "спиши",
    "списать",
    "проведи",
    "провести",
    "поступление",
    "остаток",
    "остатка",
    "минимальный",
    "минимум",
    "sku",
    "артикул"
)
