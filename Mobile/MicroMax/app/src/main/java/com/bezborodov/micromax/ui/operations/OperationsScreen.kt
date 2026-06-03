package com.bezborodov.micromax.ui.operations

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.ArrowDownward
import androidx.compose.material.icons.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.ArrowUpward
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.FilterList
import androidx.compose.material.icons.outlined.SwapHoriz
import androidx.compose.material.icons.outlined.Tune
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.data.OperationDto
import com.bezborodov.micromax.data.ProductDto
import com.bezborodov.micromax.data.StockDto
import com.bezborodov.micromax.ui.components.Accent
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.components.SearchBorder
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState
import java.math.BigDecimal
import java.time.LocalDate
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import java.util.Locale

private val RussianLocale = Locale.forLanguageTag("ru")
private val DayFormatter = DateTimeFormatter.ofPattern("d MMMM yyyy 'г.'", RussianLocale)
private const val MobileCommentLabel = "Операция из мобильного приложения"

enum class OperationType(
    val apiName: String,
    val title: String,
    val description: String,
    val accentColor: Color,
    val icon: ImageVector,
    val amountPrefix: String,
    val quantityLabel: String
) {
    Receive(
        apiName = "Receive",
        title = "Приход",
        description = "Поступление товара в выбранную ячейку.",
        accentColor = Color(0xFF61A5FA),
        icon = Icons.Outlined.ArrowDownward,
        amountPrefix = "+",
        quantityLabel = "Количество"
    ),
    WriteOff(
        apiName = "WriteOff",
        title = "Расход",
        description = "Списание товара из ячейки хранения.",
        accentColor = Color(0xFFE86A72),
        icon = Icons.Outlined.ArrowUpward,
        amountPrefix = "-",
        quantityLabel = "Количество"
    ),
    Move(
        apiName = "Move",
        title = "Перемещение",
        description = "Перенос остатка между ячейками.",
        accentColor = Color(0xFFE4A93E),
        icon = Icons.Outlined.SwapHoriz,
        amountPrefix = "→",
        quantityLabel = "Количество"
    ),
    Adjust(
        apiName = "Adjust",
        title = "Корректировка",
        description = "Установка итогового остатка в ячейке.",
        accentColor = Color(0xFF5FC8B4),
        icon = Icons.Outlined.Tune,
        amountPrefix = "→",
        quantityLabel = "Итоговое количество"
    );

    companion object {
        fun fromApiName(value: String): OperationType {
            return values().firstOrNull { it.apiName.equals(value, ignoreCase = true) } ?: Move
        }
    }
}

enum class OperationFilter(val title: String) {
    All("Все"),
    Receive("Приход"),
    WriteOff("Расход"),
    Move("Перемещение"),
    Adjust("Корректировка");

    fun matches(type: OperationType): Boolean {
        return when (this) {
            All -> true
            Receive -> type == OperationType.Receive
            WriteOff -> type == OperationType.WriteOff
            Move -> type == OperationType.Move
            Adjust -> type == OperationType.Adjust
        }
    }
}

data class OperationDraft(
    val type: OperationType,
    val product: ProductDto,
    val sourceCell: CellDto?,
    val targetCell: CellDto?,
    val quantity: Double,
    val comment: String?
)

private sealed interface OperationsMode {
    object List : OperationsMode
    data class Create(val type: OperationType) : OperationsMode
    data class Confirm(val draft: OperationDraft) : OperationsMode
}

private sealed interface OperationListItem {
    data class DateHeader(val title: String) : OperationListItem
    data class OperationEntry(val operation: OperationDto) : OperationListItem
}

private data class OperationEditorState(
    val mode: OperationsMode,
    val activeType: OperationType,
    val selectedProduct: ProductDto?,
    val selectedSourceCell: CellDto?,
    val selectedTargetCell: CellDto?
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OperationsScreen(
    state: HomeUiState,
    requestedOperationType: OperationType? = null,
    onRequestedOperationConsumed: () -> Unit = {},
    onReceive: (productId: Int, targetCellId: Int, quantity: Double, comment: String?) -> Unit,
    onWriteOff: (productId: Int, sourceCellId: Int, quantity: Double, comment: String?) -> Unit,
    onMove: (productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double, comment: String?) -> Unit,
    onAdjust: (productId: Int, targetCellId: Int, targetQuantity: Double, comment: String?) -> Unit
) {
    val products = state.snapshot.products
    val cells = state.snapshot.cells
    val stocks = state.snapshot.stocks
    val initialEditorState = buildInitialEditorState(
        requestedOperationType = requestedOperationType,
        products = products,
        cells = cells,
        stocks = stocks
    )

    // Инициализируем экран сразу нужной формой, чтобы не было краткого показа списка операций.
    var mode by remember { mutableStateOf(initialEditorState.mode) }
    var filter by remember { mutableStateOf(OperationFilter.All) }
    var isTypeSheetVisible by remember { mutableStateOf(false) }

    var activeType by remember { mutableStateOf(initialEditorState.activeType) }
    var selectedProduct by remember { mutableStateOf(initialEditorState.selectedProduct) }
    var selectedSourceCell by remember { mutableStateOf(initialEditorState.selectedSourceCell) }
    var selectedTargetCell by remember { mutableStateOf(initialEditorState.selectedTargetCell) }
    var quantity by remember { mutableStateOf("") }
    var comment by remember { mutableStateOf("") }
    var validationMessage by remember { mutableStateOf<String?>(null) }

    fun availableQuantity(product: ProductDto?, cell: CellDto?): Double {
        return calculateAvailableQuantity(
            stocks = stocks,
            product = product,
            cell = cell
        )
    }

    fun sourceCells(product: ProductDto?): List<CellDto> {
        return calculateSourceCells(
            cells = cells,
            stocks = stocks,
            product = product
        )
    }

    fun syncSelections(type: OperationType) {
        val normalizedProduct = selectedProduct?.takeIf { it in products } ?: products.firstOrNull()
        selectedProduct = normalizedProduct
        val stockCells = sourceCells(normalizedProduct)

        when (type) {
            OperationType.Receive -> {
                selectedSourceCell = null
                if (selectedTargetCell !in cells) {
                    selectedTargetCell = cells.firstOrNull()
                }
            }

            OperationType.WriteOff -> {
                selectedTargetCell = null
                if (selectedSourceCell !in stockCells) {
                    selectedSourceCell = stockCells.firstOrNull()
                }
            }

            OperationType.Move -> {
                if (selectedSourceCell !in stockCells) {
                    selectedSourceCell = stockCells.firstOrNull()
                }
                val targetOptions = cells.filter { it.id != selectedSourceCell?.id }
                if (selectedTargetCell !in targetOptions) {
                    selectedTargetCell = targetOptions.firstOrNull()
                }
            }

            OperationType.Adjust -> {
                selectedSourceCell = null
                if (selectedTargetCell !in cells) {
                    selectedTargetCell = cells.firstOrNull()
                }
            }
        }
    }

    fun resetDraft(type: OperationType) {
        activeType = type
        selectedProduct = products.firstOrNull()
        selectedSourceCell = null
        selectedTargetCell = null
        quantity = ""
        comment = ""
        validationMessage = null
        syncSelections(type)
        mode = OperationsMode.Create(type)
    }

    LaunchedEffect(products, cells, stocks, activeType) {
        if (mode is OperationsMode.Create || mode is OperationsMode.Confirm) {
            syncSelections(activeType)
        }
    }

    LaunchedEffect(requestedOperationType) {
        val requestedType = requestedOperationType ?: return@LaunchedEffect
        resetDraft(requestedType)
        onRequestedOperationConsumed()
    }

    fun buildDraft(): OperationDraft? {
        val product = selectedProduct
        val sourceCell = selectedSourceCell
        val targetCell = selectedTargetCell
        val parsedQuantity = quantity.replace(',', '.').toDoubleOrNull()

        validationMessage = when {
            product == null -> "Выберите номенклатуру."
            parsedQuantity == null -> "Введите корректное количество."
            activeType == OperationType.Adjust && parsedQuantity < 0.0 -> "Итоговый остаток не может быть отрицательным."
            activeType != OperationType.Adjust && parsedQuantity <= 0.0 -> "Количество должно быть положительным."
            activeType == OperationType.Receive && targetCell == null -> "Выберите ячейку размещения."
            activeType == OperationType.WriteOff && sourceCell == null -> "Выберите исходную ячейку."
            activeType == OperationType.Move && sourceCell == null -> "Выберите ячейку-источник."
            activeType == OperationType.Move && targetCell == null -> "Выберите ячейку назначения."
            activeType == OperationType.Move && sourceCell?.id == targetCell?.id -> "Ячейки источника и назначения должны отличаться."
            activeType == OperationType.WriteOff && parsedQuantity > availableQuantity(product, sourceCell) ->
                "Количество превышает доступный остаток в выбранной ячейке."
            activeType == OperationType.Move && parsedQuantity > availableQuantity(product, sourceCell) ->
                "Количество превышает доступный остаток в ячейке-источнике."
            activeType == OperationType.Adjust && targetCell == null -> "Выберите ячейку корректировки."
            activeType == OperationType.Adjust && parsedQuantity == availableQuantity(product, targetCell) ->
                "Текущее значение уже совпадает с указанным остатком."
            else -> null
        }

        if (validationMessage != null || product == null || parsedQuantity == null) {
            return null
        }

        return OperationDraft(
            type = activeType,
            product = product,
            sourceCell = sourceCell,
            targetCell = targetCell,
            quantity = parsedQuantity,
            comment = comment.trim().ifBlank { null }
        )
    }

    fun submitDraft(draft: OperationDraft) {
        when (draft.type) {
            OperationType.Receive -> onReceive(
                draft.product.id,
                requireNotNull(draft.targetCell).id,
                draft.quantity,
                draft.comment
            )

            OperationType.WriteOff -> onWriteOff(
                draft.product.id,
                requireNotNull(draft.sourceCell).id,
                draft.quantity,
                draft.comment
            )

            OperationType.Move -> onMove(
                draft.product.id,
                requireNotNull(draft.sourceCell).id,
                requireNotNull(draft.targetCell).id,
                draft.quantity,
                draft.comment
            )

            OperationType.Adjust -> onAdjust(
                draft.product.id,
                requireNotNull(draft.targetCell).id,
                draft.quantity,
                draft.comment
            )
        }
        mode = OperationsMode.List
    }

    Box(modifier = Modifier.fillMaxSize()) {
        when (val currentMode = mode) {
            OperationsMode.List -> OperationListScreen(
                operations = state.snapshot.operations,
                filter = filter,
                onFilterSelected = { filter = it },
                onAddClick = { isTypeSheetVisible = true }
            )

            is OperationsMode.Create -> OperationCreateScreen(
                type = currentMode.type,
                products = products,
                cells = cells,
                sourceCells = sourceCells(selectedProduct),
                selectedProduct = selectedProduct,
                selectedSourceCell = selectedSourceCell,
                selectedTargetCell = selectedTargetCell,
                quantity = quantity,
                comment = comment,
                validationMessage = validationMessage,
                availableQuantity = ::availableQuantity,
                onClose = {
                    validationMessage = null
                    mode = OperationsMode.List
                },
                onProductSelected = {
                    selectedProduct = it
                    validationMessage = null
                    syncSelections(activeType)
                },
                onSourceCellSelected = {
                    selectedSourceCell = it
                    validationMessage = null
                    syncSelections(activeType)
                },
                onTargetCellSelected = {
                    selectedTargetCell = it
                    validationMessage = null
                },
                onQuantityChanged = {
                    quantity = it
                    validationMessage = null
                },
                onCommentChanged = {
                    comment = it
                    validationMessage = null
                },
                onProceed = {
                    buildDraft()?.let { mode = OperationsMode.Confirm(it) }
                }
            )

            is OperationsMode.Confirm -> OperationConfirmScreen(
                draft = currentMode.draft,
                isSubmitting = state.isOperationSubmitting,
                availableInCell = availableQuantity(currentMode.draft.product, currentMode.draft.targetCell),
                onBack = {
                    mode = OperationsMode.Create(currentMode.draft.type)
                },
                onConfirm = { submitDraft(currentMode.draft) }
            )
        }

        if (isTypeSheetVisible) {
            OperationTypeSheet(
                onDismiss = { isTypeSheetVisible = false },
                onTypeSelected = {
                    isTypeSheetVisible = false
                    resetDraft(it)
                }
            )
        }
    }
}

@Composable
private fun OperationListScreen(
    operations: List<OperationDto>,
    filter: OperationFilter,
    onFilterSelected: (OperationFilter) -> Unit,
    onAddClick: () -> Unit
) {
    var filterExpanded by remember { mutableStateOf(false) }
    val filteredOperations = operations.filter { operation ->
        filter.matches(OperationType.fromApiName(operation.type))
    }
    val items = remember(filteredOperations) { buildOperationListItems(filteredOperations) }

    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(14.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Spacer(modifier = Modifier.width(44.dp))
                Text(
                    text = "Транзакции",
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.weight(1f),
                    textAlign = TextAlign.Center
                )
                Box(
                    modifier = Modifier
                        .size(44.dp)
                        .clip(CircleShape)
                        .background(Color.White)
                        .border(1.dp, SearchBorder, CircleShape)
                        .clickable(onClick = onAddClick),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Add,
                        contentDescription = "Новая транзакция",
                        tint = Color.Black
                    )
                }
            }
        }

        item {
            Box {
                OutlinedButton(onClick = { filterExpanded = true }) {
                    Icon(
                        imageVector = Icons.Outlined.FilterList,
                        contentDescription = null,
                        tint = TextMuted
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(filter.title)
                }

                DropdownMenu(
                    expanded = filterExpanded,
                    onDismissRequest = { filterExpanded = false }
                ) {
                    OperationFilter.values().forEach { option ->
                        DropdownMenuItem(
                            text = { Text(option.title) },
                            onClick = {
                                onFilterSelected(option)
                                filterExpanded = false
                            }
                        )
                    }
                }
            }
        }

        if (items.isEmpty()) {
            item {
                EmptyStateCard(
                    title = "Транзакции не найдены",
                    description = "Измените фильтр или создайте новую складскую операцию."
                )
            }
        } else {
            items.forEach { listItem ->
                when (listItem) {
                    is OperationListItem.DateHeader -> item {
                        Text(
                            text = listItem.title,
                            style = MaterialTheme.typography.titleLarge,
                            color = TextMuted,
                            modifier = Modifier.padding(top = 4.dp, bottom = 2.dp)
                        )
                    }

                    is OperationListItem.OperationEntry -> item {
                        OperationRowCard(listItem.operation)
                    }
                }
            }
        }
    }
}

@Composable
private fun OperationRowCard(operation: OperationDto) {
    val type = OperationType.fromApiName(operation.type)
    val visibleComment = operation.comment?.takeUnless { it == MobileCommentLabel }
    val summary = when (type) {
        OperationType.Receive -> "В ячейку ${operation.targetCell.orEmpty()}"
        OperationType.WriteOff -> "Из ячейки ${operation.sourceCell.orEmpty()}"
        OperationType.Move -> "${operation.sourceCell.orEmpty()} → ${operation.targetCell.orEmpty()}"
        OperationType.Adjust -> visibleComment ?: "Корректировка остатка"
    }

    Card(
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 3.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(42.dp)
                        .clip(CircleShape)
                        .background(type.accentColor.copy(alpha = 0.12f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = type.icon,
                        contentDescription = null,
                        tint = type.accentColor
                    )
                }

                Spacer(modifier = Modifier.width(14.dp))

                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = type.title,
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = operation.productName,
                        style = MaterialTheme.typography.titleMedium,
                        color = Color(0xFF222222)
                    )
                }

                Text(
                    text = "${type.amountPrefix} ${formatQuantity(operation.quantity)}",
                    style = MaterialTheme.typography.headlineSmall,
                    color = type.accentColor,
                    fontWeight = FontWeight.SemiBold
                )
            }

            Text(
                text = summary,
                style = MaterialTheme.typography.bodyLarge,
                color = TextMuted
            )

            if (!visibleComment.isNullOrBlank() && type != OperationType.Adjust) {
                Text(
                    text = visibleComment,
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            }
        }
    }
}

@Composable
private fun OperationCreateScreen(
    type: OperationType,
    products: List<ProductDto>,
    cells: List<CellDto>,
    sourceCells: List<CellDto>,
    selectedProduct: ProductDto?,
    selectedSourceCell: CellDto?,
    selectedTargetCell: CellDto?,
    quantity: String,
    comment: String,
    validationMessage: String?,
    availableQuantity: (ProductDto?, CellDto?) -> Double,
    onClose: () -> Unit,
    onProductSelected: (ProductDto) -> Unit,
    onSourceCellSelected: (CellDto) -> Unit,
    onTargetCellSelected: (CellDto) -> Unit,
    onQuantityChanged: (String) -> Unit,
    onCommentChanged: (String) -> Unit,
    onProceed: () -> Unit
) {
    val targetOptions = if (type == OperationType.Move) {
        cells.filter { it.id != selectedSourceCell?.id }
    } else {
        cells
    }

    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(14.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(44.dp)
                        .clip(CircleShape)
                        .clickable(onClick = onClose),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Close,
                        contentDescription = "Закрыть"
                    )
                }

                Text(
                    text = "Новая транзакция",
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.weight(1f),
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.width(44.dp))
            }
        }

        item {
            Card(
                colors = CardDefaults.cardColors(containerColor = Color.White),
                shape = RoundedCornerShape(10.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp),
                    verticalArrangement = Arrangement.spacedBy(14.dp)
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = type.title,
                            style = MaterialTheme.typography.headlineLarge,
                            color = type.accentColor,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.weight(1f)
                        )
                        Box(
                            modifier = Modifier
                                .clip(RoundedCornerShape(12.dp))
                                .background(ScreenBg)
                                .padding(horizontal = 14.dp, vertical = 8.dp)
                        ) {
                            Text(
                                text = "Сейчас",
                                style = MaterialTheme.typography.titleMedium,
                                color = TextMuted
                            )
                        }
                    }

                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(3.dp)
                            .clip(RoundedCornerShape(99.dp))
                            .background(type.accentColor)
                    )

                    if (validationMessage != null) {
                        Text(
                            text = validationMessage,
                            color = MaterialTheme.colorScheme.error,
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }

                    when (type) {
                        OperationType.Receive -> {
                            DropdownSelectionRow(
                                label = "Куда",
                                value = selectedTargetCell?.let { "${it.code} · ${it.name}" } ?: "Выбрать",
                                options = cells,
                                optionLabel = { "${it.code} · ${it.name}" },
                                enabled = cells.isNotEmpty(),
                                onSelected = onTargetCellSelected
                            )
                        }

                        OperationType.WriteOff -> {
                            DropdownSelectionRow(
                                label = "Откуда",
                                value = selectedSourceCell?.let {
                                    "${it.code} · доступно ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                } ?: "Выбрать",
                                options = sourceCells,
                                optionLabel = {
                                    "${it.code} · ${it.name} · ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                },
                                enabled = sourceCells.isNotEmpty(),
                                onSelected = onSourceCellSelected
                            )
                        }

                        OperationType.Move -> {
                            DropdownSelectionRow(
                                label = "Откуда",
                                value = selectedSourceCell?.let {
                                    "${it.code} · доступно ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                } ?: "Выбрать",
                                options = sourceCells,
                                optionLabel = {
                                    "${it.code} · ${it.name} · ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                },
                                enabled = sourceCells.isNotEmpty(),
                                onSelected = onSourceCellSelected
                            )
                            DropdownSelectionRow(
                                label = "Куда",
                                value = selectedTargetCell?.let { "${it.code} · ${it.name}" } ?: "Выбрать",
                                options = targetOptions,
                                optionLabel = { "${it.code} · ${it.name}" },
                                enabled = targetOptions.isNotEmpty(),
                                onSelected = onTargetCellSelected
                            )
                        }

                        OperationType.Adjust -> {
                            DropdownSelectionRow(
                                label = "Ячейка",
                                value = selectedTargetCell?.let {
                                    "${it.code} · ${it.name} · сейчас ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                } ?: "Выбрать",
                                options = cells,
                                optionLabel = {
                                    "${it.code} · ${it.name} · сейчас ${formatQuantity(availableQuantity(selectedProduct, it))}"
                                },
                                enabled = cells.isNotEmpty(),
                                onSelected = onTargetCellSelected
                            )
                        }
                    }

                    DropdownSelectionRow(
                        label = "Товар",
                        value = selectedProduct?.let { "${it.name} · ${it.sku}" } ?: "Выбрать",
                        options = products,
                        optionLabel = { "${it.name} · ${it.sku}" },
                        enabled = products.isNotEmpty(),
                        onSelected = onProductSelected
                    )

                    selectedProduct?.let { product ->
                        SelectedProductCard(
                            product = product,
                            quantityText = when (type) {
                                OperationType.Adjust -> {
                                    selectedTargetCell?.let { "Сейчас ${formatQuantity(availableQuantity(product, it))} ${product.unit}" }
                                }

                                OperationType.WriteOff,
                                OperationType.Move -> {
                                    selectedSourceCell?.let { "Доступно ${formatQuantity(availableQuantity(product, it))} ${product.unit}" }
                                }

                                OperationType.Receive -> null
                            }
                        )
                    }

                    OutlinedTextField(
                        value = quantity,
                        onValueChange = onQuantityChanged,
                        label = { Text(type.quantityLabel) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )

                    OutlinedTextField(
                        value = comment,
                        onValueChange = onCommentChanged,
                        label = { Text("Примечание") },
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            }
        }

        item {
            Button(
                onClick = onProceed,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(bottom = 12.dp)
            ) {
                Text("Проверить и продолжить")
            }
        }
    }
}

@Composable
private fun OperationConfirmScreen(
    draft: OperationDraft,
    isSubmitting: Boolean,
    availableInCell: Double,
    onBack: () -> Unit,
    onConfirm: () -> Unit
) {
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(14.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Text(
                text = "Подтверждение",
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(top = 8.dp)
            )
        }

        item {
            Card(
                colors = CardDefaults.cardColors(containerColor = Color.White),
                shape = RoundedCornerShape(10.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    ConfirmRow("Тип операции", draft.type.title)
                    ConfirmRow("Номенклатура", "${draft.product.name} · ${draft.product.sku}")
                    ConfirmRow(
                        draft.type.quantityLabel,
                        "${formatQuantity(draft.quantity)} ${draft.product.unit}"
                    )
                    draft.sourceCell?.let { ConfirmRow("Откуда", "${it.code} · ${it.name}") }
                    draft.targetCell?.let { ConfirmRow("Куда", "${it.code} · ${it.name}") }
                    if (draft.type == OperationType.Adjust && draft.targetCell != null) {
                        ConfirmRow("Текущий остаток", "${formatQuantity(availableInCell)} ${draft.product.unit}")
                    }
                    if (!draft.comment.isNullOrBlank()) {
                        ConfirmRow("Примечание", draft.comment)
                    }
                }
            }
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                OutlinedButton(
                    onClick = onBack,
                    enabled = !isSubmitting,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("Изменить")
                }
                Button(
                    onClick = onConfirm,
                    enabled = !isSubmitting,
                    modifier = Modifier.weight(1f)
                ) {
                    Text(if (isSubmitting) "Отправка..." else "Подтвердить")
                }
            }
        }
    }
}

@Composable
private fun ConfirmRow(title: String, value: String) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(
            text = title,
            style = MaterialTheme.typography.labelLarge,
            color = TextMuted
        )
        Text(
            text = value,
            style = MaterialTheme.typography.titleMedium,
            color = Color(0xFF202020),
            fontWeight = FontWeight.SemiBold
        )
    }
}

@Composable
private fun SelectedProductCard(
    product: ProductDto,
    quantityText: String?
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = ScreenBg),
        shape = RoundedCornerShape(10.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(52.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .background(Color(0xFFE3E3E7))
            )

            Spacer(modifier = Modifier.width(12.dp))

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = product.name,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = product.sku,
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            }

            if (quantityText != null) {
                Text(
                    text = quantityText,
                    style = MaterialTheme.typography.bodyMedium,
                    color = Accent,
                    textAlign = TextAlign.End
                )
            }
        }
    }
}

@Composable
private fun <T> DropdownSelectionRow(
    label: String,
    value: String,
    options: List<T>,
    optionLabel: (T) -> String,
    enabled: Boolean,
    onSelected: (T) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    Box(modifier = Modifier.fillMaxWidth()) {
        Column {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable(enabled = enabled, onClick = { expanded = true })
                    .padding(vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = label,
                    style = MaterialTheme.typography.titleLarge,
                    color = Color(0xFF444444),
                    modifier = Modifier.weight(1f)
                )
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleMedium,
                    color = if (enabled) Color(0xFF222222) else TextMuted,
                    textAlign = TextAlign.End,
                    modifier = Modifier.weight(1f)
                )
                Spacer(modifier = Modifier.width(10.dp))
                Icon(
                    imageVector = Icons.Outlined.ArrowForwardIos,
                    contentDescription = null,
                    tint = TextMuted,
                    modifier = Modifier.size(14.dp)
                )
            }

            HorizontalDivider(color = SearchBorder)
        }

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            options.forEach { option ->
                DropdownMenuItem(
                    text = { Text(optionLabel(option)) },
                    onClick = {
                        onSelected(option)
                        expanded = false
                    }
                )
            }
        }
    }
}

@Composable
private fun EmptyStateCard(title: String, description: String) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Color.White),
        shape = RoundedCornerShape(10.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier.padding(horizontal = 18.dp, vertical = 18.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold
            )
            EmptyStateText(description)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun OperationTypeSheet(
    onDismiss: () -> Unit,
    onTypeSelected: (OperationType) -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = "Выберите транзакцию",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )

            OperationType.values().forEach { type ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(10.dp))
                        .clickable { onTypeSelected(type) }
                        .padding(vertical = 14.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .size(42.dp)
                            .clip(CircleShape)
                            .background(type.accentColor.copy(alpha = 0.12f)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = type.icon,
                            contentDescription = null,
                            tint = type.accentColor
                        )
                    }

                    Spacer(modifier = Modifier.width(14.dp))

                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = type.title,
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.SemiBold
                        )
                        Text(
                            text = type.description,
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextMuted
                        )
                    }

                    Icon(
                        imageVector = Icons.Outlined.ArrowForwardIos,
                        contentDescription = null,
                        tint = TextMuted,
                        modifier = Modifier.size(14.dp)
                    )
                }
            }
        }
    }
}

private fun buildOperationListItems(operations: List<OperationDto>): List<OperationListItem> {
    val result = mutableListOf<OperationListItem>()
    var lastDate: LocalDate? = null

    operations.forEach { operation ->
        val currentDate = parseOperationDate(operation.createdAt)?.toLocalDate()
        if (currentDate != lastDate) {
            result += OperationListItem.DateHeader(
                currentDate?.format(DayFormatter) ?: "Без даты"
            )
            lastDate = currentDate
        }
        result += OperationListItem.OperationEntry(operation)
    }

    return result
}

private fun parseOperationDate(value: String): OffsetDateTime? {
    return runCatching { OffsetDateTime.parse(value) }.getOrNull()
}

private fun buildInitialEditorState(
    requestedOperationType: OperationType?,
    products: List<ProductDto>,
    cells: List<CellDto>,
    stocks: List<StockDto>
): OperationEditorState {
    val activeType = requestedOperationType ?: OperationType.Receive
    val selectedProduct = products.firstOrNull()
    val sourceCells = calculateSourceCells(
        cells = cells,
        stocks = stocks,
        product = selectedProduct
    )
    val selectedSourceCell = when (activeType) {
        OperationType.WriteOff,
        OperationType.Move -> sourceCells.firstOrNull()

        OperationType.Receive,
        OperationType.Adjust -> null
    }
    val selectedTargetCell = when (activeType) {
        OperationType.Receive,
        OperationType.Adjust -> cells.firstOrNull()

        OperationType.Move -> cells.firstOrNull { it.id != selectedSourceCell?.id }
        OperationType.WriteOff -> null
    }

    return OperationEditorState(
        mode = requestedOperationType?.let(OperationsMode::Create) ?: OperationsMode.List,
        activeType = activeType,
        selectedProduct = selectedProduct,
        selectedSourceCell = selectedSourceCell,
        selectedTargetCell = selectedTargetCell
    )
}

private fun calculateAvailableQuantity(
    stocks: List<StockDto>,
    product: ProductDto?,
    cell: CellDto?
): Double {
    if (product == null || cell == null) return 0.0
    return stocks
        .filter { it.sku == product.sku && it.cellCode == cell.code }
        .sumOf(StockDto::quantity)
}

private fun calculateSourceCells(
    cells: List<CellDto>,
    stocks: List<StockDto>,
    product: ProductDto?
): List<CellDto> {
    return cells.filter { cell ->
        calculateAvailableQuantity(
            stocks = stocks,
            product = product,
            cell = cell
        ) > 0.0
    }
}

private fun formatQuantity(value: Double): String {
    return BigDecimal.valueOf(value).stripTrailingZeros().toPlainString()
}
