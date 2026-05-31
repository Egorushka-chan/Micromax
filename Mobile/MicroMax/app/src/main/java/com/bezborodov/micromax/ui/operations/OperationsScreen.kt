package com.bezborodov.micromax.ui.operations

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowForwardIos
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.data.ProductDto
import com.bezborodov.micromax.ui.components.Accent
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.CompactInput
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.HomeMenuIcon
import com.bezborodov.micromax.ui.components.MenuLeadingIcon
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.ScreenBg
import com.bezborodov.micromax.ui.components.SearchBorder
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.SimpleTitle
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState

enum class OperationType(
    val title: String,
    val subtitle: String,
    val icon: HomeMenuIcon
) {
    Receive(
        title = "Приход",
        subtitle = "Принять товар в выбранную ячейку",
        icon = HomeMenuIcon.Receive
    ),
    WriteOff(
        title = "Расход",
        subtitle = "Списать товар из ячейки хранения",
        icon = HomeMenuIcon.WriteOff
    ),
    Move(
        title = "Перемещение",
        subtitle = "Перенести остаток между ячейками",
        icon = HomeMenuIcon.Move
    )
}

data class OperationDraft(
    val type: OperationType,
    val product: ProductDto,
    val sourceCell: CellDto?,
    val targetCell: CellDto?,
    val quantity: Double
)

@Composable
fun OperationsScreen(
    state: HomeUiState,
    onReceive: (productId: Int, targetCellId: Int, quantity: Double) -> Unit,
    onWriteOff: (productId: Int, sourceCellId: Int, quantity: Double) -> Unit,
    onMove: (productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double) -> Unit
) {
    val products = state.snapshot.products
    val cells = state.snapshot.cells
    var operationType by remember { mutableStateOf(OperationType.Receive) }
    var selectedProduct by remember(products) { mutableStateOf(products.firstOrNull()) }
    var selectedSourceCell by remember(cells) { mutableStateOf(cells.firstOrNull()) }
    var selectedTargetCell by remember(cells) { mutableStateOf(cells.firstOrNull()) }
    var quantity by remember { mutableStateOf("1") }
    var validationMessage by remember { mutableStateOf<String?>(null) }
    var pendingDraft by remember { mutableStateOf<OperationDraft?>(null) }

    val sourceCells = cells.filter { cell ->
        val product = selectedProduct
        product != null && state.snapshot.stocks.any {
            it.sku == product.sku && it.cellCode == cell.code && it.quantity > 0.0
        }
    }.ifEmpty { cells }

    LaunchedEffect(operationType, selectedProduct, sourceCells, cells) {
        if (operationType == OperationType.Receive) {
            if (selectedTargetCell == null || selectedTargetCell !in cells) {
                selectedTargetCell = cells.firstOrNull()
            }
        } else {
            if (selectedSourceCell == null || selectedSourceCell !in sourceCells) {
                selectedSourceCell = sourceCells.firstOrNull()
            }
            if (operationType == OperationType.Move && (selectedTargetCell == null || selectedTargetCell !in cells)) {
                selectedTargetCell = cells.firstOrNull { it.id != selectedSourceCell?.id } ?: cells.firstOrNull()
            }
        }
    }

    fun availableQuantity(product: ProductDto?, cell: CellDto?): Double {
        if (product == null || cell == null) return 0.0
        return state.snapshot.stocks
            .filter { it.sku == product.sku && it.cellCode == cell.code }
            .sumOf { it.quantity }
    }

    fun prepareConfirmation() {
        val product = selectedProduct
        val parsedQuantity = quantity.replace(',', '.').toDoubleOrNull()
        val sourceCell = selectedSourceCell
        val targetCell = selectedTargetCell

        validationMessage = when {
            product == null -> "Выберите номенклатуру."
            parsedQuantity == null || parsedQuantity <= 0.0 -> "Введите положительное количество."
            operationType != OperationType.Receive && sourceCell == null -> "Выберите исходную ячейку."
            operationType != OperationType.WriteOff && targetCell == null -> "Выберите целевую ячейку."
            operationType == OperationType.Move && sourceCell?.id == targetCell?.id -> "Исходная и целевая ячейки должны отличаться."
            operationType != OperationType.Receive && parsedQuantity > availableQuantity(product, sourceCell) -> "Количество превышает доступный остаток в исходной ячейке."
            else -> null
        }

        if (validationMessage == null && product != null && parsedQuantity != null) {
            pendingDraft = OperationDraft(
                type = operationType,
                product = product,
                sourceCell = if (operationType == OperationType.Receive) null else sourceCell,
                targetCell = if (operationType == OperationType.WriteOff) null else targetCell,
                quantity = parsedQuantity
            )
        }
    }

    fun submitDraft(draft: OperationDraft) {
        when (draft.type) {
            OperationType.Receive -> onReceive(
                draft.product.id,
                requireNotNull(draft.targetCell).id,
                draft.quantity
            )

            OperationType.WriteOff -> onWriteOff(
                draft.product.id,
                requireNotNull(draft.sourceCell).id,
                draft.quantity
            )

            OperationType.Move -> onMove(
                draft.product.id,
                requireNotNull(draft.sourceCell).id,
                requireNotNull(draft.targetCell).id,
                draft.quantity
            )
        }
        pendingDraft = null
    }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        item { SimpleTitle("Операции") }
        item {
            SectionCard(title = "Новая операция") {
                if (validationMessage != null) {
                    Text(
                        text = validationMessage!!,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }

                OperationType.entries.forEach { type ->
                    OperationTypeRow(
                        type = type,
                        selected = operationType == type,
                        onClick = {
                            operationType = type
                            pendingDraft = null
                            validationMessage = null
                        }
                    )
                }

                DropdownSelector(
                    label = "Номенклатура",
                    value = selectedProduct?.let { "${it.name} · ${it.sku}" } ?: "Нет товаров",
                    options = products,
                    optionLabel = { "${it.name} · ${it.sku}" },
                    enabled = products.isNotEmpty(),
                    onSelected = {
                        selectedProduct = it
                        pendingDraft = null
                        validationMessage = null
                    }
                )

                if (operationType != OperationType.Receive) {
                    DropdownSelector(
                        label = "Исходная ячейка",
                        value = selectedSourceCell?.let {
                            "${it.code} · доступно ${availableQuantity(selectedProduct, it)} ${selectedProduct?.unit.orEmpty()}"
                        } ?: "Нет ячеек с остатками",
                        options = sourceCells,
                        optionLabel = {
                            "${it.code} · ${it.name} · ${availableQuantity(selectedProduct, it)} ${selectedProduct?.unit.orEmpty()}"
                        },
                        enabled = sourceCells.isNotEmpty(),
                        onSelected = {
                            selectedSourceCell = it
                            pendingDraft = null
                            validationMessage = null
                        }
                    )
                }

                if (operationType != OperationType.WriteOff) {
                    DropdownSelector(
                        label = "Целевая ячейка",
                        value = selectedTargetCell?.let { "${it.code} · ${it.name}" } ?: "Нет ячеек",
                        options = cells,
                        optionLabel = { "${it.code} · ${it.name}" },
                        enabled = cells.isNotEmpty(),
                        onSelected = {
                            selectedTargetCell = it
                            pendingDraft = null
                            validationMessage = null
                        }
                    )
                }

                CompactInput(value = quantity, onValueChange = { quantity = it }, label = "Количество")

                pendingDraft?.let { draft ->
                    OperationConfirmationCard(
                        draft = draft,
                        isSubmitting = state.isOperationSubmitting,
                        onConfirm = { submitDraft(draft) },
                        onEdit = { pendingDraft = null }
                    )
                }

                Button(
                    onClick = { prepareConfirmation() },
                    enabled = !state.isOperationSubmitting,
                    modifier = Modifier.fillMaxWidth()
                ) { Text(if (pendingDraft == null) "Проверить и продолжить" else "Обновить подтверждение") }
            }
        }
        item {
            SectionCard(title = "Последние операции") {
                if (state.snapshot.operations.isEmpty()) {
                    EmptyStateText("Журнал операций пока пуст.")
                } else {
                    state.snapshot.operations.take(20).forEach { operation ->
                        PlainInfoRow(
                            title = "${operation.type}: ${operation.productName}",
                            subtitle = "${operation.sourceCell.orEmpty()} → ${operation.targetCell.orEmpty()} · ${operation.quantity}"
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun OperationTypeRow(
    type: OperationType,
    selected: Boolean,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(if (selected) Accent.copy(alpha = 0.08f) else Color.White)
            .border(
                width = 1.dp,
                color = if (selected) AccentDark else SearchBorder,
                shape = RoundedCornerShape(8.dp)
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        MenuLeadingIcon(type.icon)

        Spacer(modifier = Modifier.width(12.dp))

        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = type.title,
                style = MaterialTheme.typography.titleMedium,
                color = Color(0xFF1E1E1E),
                fontWeight = FontWeight.SemiBold
            )
            Text(
                text = type.subtitle,
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }
    }
}

@Composable
private fun <T> DropdownSelector(
    label: String,
    value: String,
    options: List<T>,
    optionLabel: (T) -> String,
    enabled: Boolean = true,
    onSelected: (T) -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = TextMuted
        )

        Box(modifier = Modifier.fillMaxWidth()) {
            OutlinedButton(
                onClick = { expanded = true },
                enabled = enabled,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = value,
                    modifier = Modifier.weight(1f),
                    style = MaterialTheme.typography.bodyMedium
                )
                Icon(
                    imageVector = Icons.Outlined.ArrowForwardIos,
                    contentDescription = null,
                    tint = TextMuted,
                    modifier = Modifier.size(14.dp)
                )
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
}

@Composable
private fun OperationConfirmationCard(
    draft: OperationDraft,
    isSubmitting: Boolean,
    onConfirm: () -> Unit,
    onEdit: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .background(ScreenBg)
            .border(1.dp, SearchBorder, RoundedCornerShape(8.dp))
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Text(
            text = "Подтверждение операции",
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold
        )
        PlainInfoRow("Тип операции", draft.type.title)
        PlainInfoRow("Номенклатура", "${draft.product.name} · ${draft.product.sku}")
        PlainInfoRow("Количество", "${draft.quantity} ${draft.product.unit}")
        if (draft.sourceCell != null) {
            PlainInfoRow("Исходная ячейка", "${draft.sourceCell.code} · ${draft.sourceCell.name}")
        }
        if (draft.targetCell != null) {
            PlainInfoRow("Целевая ячейка", "${draft.targetCell.code} · ${draft.targetCell.name}")
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            OutlinedButton(
                onClick = onEdit,
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
                Text(if (isSubmitting) "Выполнение..." else "Подтвердить")
            }
        }
    }
}
