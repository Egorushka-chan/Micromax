package com.bezborodov.micromax.ui.cells

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Inventory2
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.bezborodov.micromax.data.BarcodeDto
import com.bezborodov.micromax.data.CellDto
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.StockDto
import com.bezborodov.micromax.data.UnauthorizedException
import com.bezborodov.micromax.ui.barcodes.BarcodeEditorDialog
import com.bezborodov.micromax.ui.barcodes.BarcodeSection
import com.bezborodov.micromax.ui.components.AccentDark
import com.bezborodov.micromax.ui.components.EmptyStateText
import com.bezborodov.micromax.ui.components.PlainInfoRow
import com.bezborodov.micromax.ui.components.SectionCard
import com.bezborodov.micromax.ui.components.TextMuted
import com.bezborodov.micromax.ui.home.HomeUiState
import com.bezborodov.micromax.ui.scanner.ScannedBarcode
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToLong
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

private enum class CellsDestination {
    List,
    Details
}

@Composable
fun CellsScreen(
    state: HomeUiState,
    warehouseId: Int,
    apiClient: MicroMaxApiClient,
    onSessionExpired: () -> Unit,
    canExecuteOperations: Boolean,
    canManageCellBarcodes: (Int?) -> Boolean,
    requestedCellId: Int?,
    onRequestedCellConsumed: () -> Unit,
    onOpenScanner: (String, (ScannedBarcode) -> Unit) -> Unit,
    onOpenOperations: () -> Unit
) {
    var destination by rememberSaveable { mutableStateOf(CellsDestination.List.name) }
    var selectedCellId by rememberSaveable { mutableStateOf<Int?>(null) }

    LaunchedEffect(requestedCellId, state.snapshot.cells) {
        val targetId = requestedCellId ?: return@LaunchedEffect
        if (state.snapshot.cells.any { it.id == targetId }) {
            selectedCellId = targetId
            destination = CellsDestination.Details.name
            onRequestedCellConsumed()
        }
    }

    val selectedCell = state.snapshot.cells.firstOrNull { it.id == selectedCellId }

    when (CellsDestination.valueOf(destination)) {
        CellsDestination.List -> CellsListScreen(
            cells = state.snapshot.cells,
            stocks = state.snapshot.stocks,
            onOpenCell = {
                selectedCellId = it.id
                destination = CellsDestination.Details.name
            }
        )

        CellsDestination.Details -> {
            if (selectedCell == null) {
                CellsListScreen(
                    cells = state.snapshot.cells,
                    stocks = state.snapshot.stocks,
                    onOpenCell = {
                        selectedCellId = it.id
                        destination = CellsDestination.Details.name
                    }
                )
            } else {
                CellDetailsScreen(
                    cell = selectedCell,
                    stocks = state.snapshot.stocks.filter { it.cellCode == selectedCell.code && it.quantity > 0.0 },
                    warehouseId = warehouseId,
                    apiClient = apiClient,
                    onSessionExpired = onSessionExpired,
                    canManageBarcodes = canManageCellBarcodes(selectedCell.warehouseId),
                    canExecuteOperations = canExecuteOperations,
                    onOpenScanner = onOpenScanner,
                    onBack = { destination = CellsDestination.List.name },
                    onOpenOperations = onOpenOperations
                )
            }
        }
    }
}

@Composable
private fun CellsListScreen(
    cells: List<CellDto>,
    stocks: List<StockDto>,
    onOpenCell: (CellDto) -> Unit
) {
    var query by rememberSaveable { mutableStateOf("") }
    val normalizedQuery = query.trim()

    val filteredCells = remember(cells, stocks, normalizedQuery) {
        cells.filter { cell ->
            val cellStocks = stocks.filter { it.cellCode == cell.code && it.quantity > 0.0 }
            normalizedQuery.isBlank() ||
                cell.code.contains(normalizedQuery, ignoreCase = true) ||
                cell.name.contains(normalizedQuery, ignoreCase = true) ||
                cell.zoneCode.contains(normalizedQuery, ignoreCase = true) ||
                cell.warehouseName.contains(normalizedQuery, ignoreCase = true) ||
                cellStocks.any {
                    it.productName.contains(normalizedQuery, ignoreCase = true) ||
                        it.sku.contains(normalizedQuery, ignoreCase = true)
                }
        }.sortedWith(compareBy<CellDto> { it.zoneCode.lowercase(Locale.getDefault()) }.thenBy { it.code.lowercase(Locale.getDefault()) })
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text(
                text = "Ячейки хранения",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.fillMaxWidth(),
                textAlign = TextAlign.Center
            )
        }

        item {
            androidx.compose.material3.OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Поиск по коду, зоне, складу или товару") },
                singleLine = true
            )
        }

        if (filteredCells.isEmpty()) {
            item {
                SectionCard(title = "Ячейки") {
                    EmptyStateText(
                        if (normalizedQuery.isBlank()) {
                            "Доступные ячейки пока не загружены."
                        } else {
                            "По запросу ничего не найдено."
                        }
                    )
                }
            }
        } else {
            items(filteredCells, key = { it.id }) { cell ->
                CellRow(
                    cell = cell,
                    stockCount = stocks.count { it.cellCode == cell.code && it.quantity > 0.0 },
                    totalQuantity = stocks.filter { it.cellCode == cell.code && it.quantity > 0.0 }.sumOf { it.quantity },
                    onClick = { onOpenCell(cell) }
                )
            }
        }
    }
}

@Composable
private fun CellDetailsScreen(
    cell: CellDto,
    stocks: List<StockDto>,
    warehouseId: Int,
    apiClient: MicroMaxApiClient,
    onSessionExpired: () -> Unit,
    canManageBarcodes: Boolean,
    canExecuteOperations: Boolean,
    onOpenScanner: (String, (ScannedBarcode) -> Unit) -> Unit,
    onBack: () -> Unit,
    onOpenOperations: () -> Unit
) {
    val scope = rememberCoroutineScope()

    var barcodes by remember(cell.id) { mutableStateOf<List<BarcodeDto>>(emptyList()) }
    var barcodesLoading by remember(cell.id) { mutableStateOf(true) }
    var barcodesMessage by remember(cell.id) { mutableStateOf<String?>(null) }
    var showAddBarcodeDialog by remember(cell.id) { mutableStateOf(false) }
    var barcodeToDeactivate by remember(cell.id) { mutableStateOf<BarcodeDto?>(null) }

    suspend fun loadBarcodes() {
        barcodesLoading = true
        val result = runCatching {
            withContext(Dispatchers.IO) { apiClient.getCellBarcodes(cell.id) }
        }
        result.fold(
            onSuccess = {
                barcodes = it
                barcodesLoading = false
            },
            onFailure = { error ->
                barcodesLoading = false
                if (error is UnauthorizedException) {
                    onSessionExpired()
                } else {
                    barcodesMessage = error.message ?: "Не удалось загрузить штрих-коды."
                }
            }
        )
    }

    LaunchedEffect(cell.id) {
        loadBarcodes()
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            ScreenHeader(
                title = "Карточка ячейки",
                onBack = onBack
            )
        }

        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = Color.White),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(124.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Spacer(modifier = Modifier.width(18.dp))

                    Box(
                        modifier = Modifier
                            .width(88.dp)
                            .height(88.dp)
                            .background(Color(0xFFEFF8F3), RoundedCornerShape(16.dp)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Outlined.Place,
                            contentDescription = null,
                            tint = Color(0xFF2E8B57)
                        )
                    }

                    Spacer(modifier = Modifier.width(16.dp))

                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = cell.code,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(6.dp))
                        Text(
                            text = cell.name,
                            style = MaterialTheme.typography.bodyLarge,
                            color = TextMuted
                        )
                    }

                    Spacer(modifier = Modifier.width(18.dp))
                }
            }
        }

        item {
            SectionCard(title = "Основные данные") {
                PlainInfoRow(title = "Код ячейки", subtitle = cell.code)
                PlainInfoRow(title = "Наименование", subtitle = cell.name)
                PlainInfoRow(title = "Зона хранения", subtitle = cell.zoneCode)
                PlainInfoRow(title = "Склад", subtitle = cell.warehouseName)
                PlainInfoRow(title = "Позиций в ячейке", subtitle = stocks.size.toString())
            }
        }

        item {
            BarcodeSection(
                barcodes = barcodes,
                isLoading = barcodesLoading,
                message = barcodesMessage,
                canManageBarcodes = canManageBarcodes,
                onAddBarcode = {
                    barcodesMessage = null
                    showAddBarcodeDialog = true
                },
                onDeactivateBarcode = {
                    barcodesMessage = null
                    barcodeToDeactivate = it
                }
            )
        }

        item {
            SectionCard(title = "Содержимое ячейки") {
                if (stocks.isEmpty()) {
                    EmptyStateText("В этой ячейке сейчас нет остатков.")
                } else {
                    stocks.sortedWith(compareBy({ it.productName.lowercase(Locale.getDefault()) }, { it.sku.lowercase(Locale.getDefault()) }))
                        .forEach { stock ->
                            PlainInfoRow(
                                title = stock.productName,
                                subtitle = "${stock.quantity.formatQuantity()} ${stock.unit} · ${stock.sku}"
                            )
                        }
                }
            }
        }

        item {
            Text(
                text = "Сканер только распознаёт код и открывает карточку. Изменение остатков выполняется отдельной складской операцией.",
                style = MaterialTheme.typography.bodyMedium,
                color = TextMuted
            )
        }

        if (canExecuteOperations) {
            item {
                Button(
                    onClick = onOpenOperations,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(54.dp)
                ) {
                    Icon(
                        imageVector = Icons.Outlined.Inventory2,
                        contentDescription = null
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Открыть операции")
                }
            }
        }

        item { Spacer(modifier = Modifier.height(6.dp)) }
    }

    if (showAddBarcodeDialog) {
        BarcodeEditorDialog(
            title = "Добавить штрих-код ячейки",
            confirmButtonText = "Сохранить",
            onDismiss = { showAddBarcodeDialog = false },
            onOpenScanner = { callback ->
                onOpenScanner("Сканирование штрих-кода ячейки", callback)
            },
            onConfirm = { request ->
                scope.launch {
                    barcodesLoading = true
                    val result = runCatching {
                        withContext(Dispatchers.IO) {
                            apiClient.addCellBarcode(cell.id, request)
                            apiClient.getCellBarcodes(cell.id)
                        }
                    }
                    result.fold(
                        onSuccess = {
                            barcodes = it
                            barcodesLoading = false
                            barcodesMessage = "Штрих-код привязан к ячейке."
                            showAddBarcodeDialog = false
                        },
                        onFailure = { error ->
                            barcodesLoading = false
                            if (error is UnauthorizedException) {
                                onSessionExpired()
                            } else {
                                barcodesMessage = error.message ?: "Не удалось привязать штрих-код."
                            }
                        }
                    )
                }
            }
        )
    }

    if (barcodeToDeactivate != null) {
        BarcodeDeactivateDialog(
            value = barcodeToDeactivate?.value.orEmpty(),
            onDismiss = { barcodeToDeactivate = null },
            onConfirm = {
                val currentBarcode = barcodeToDeactivate ?: return@BarcodeDeactivateDialog
                scope.launch {
                    barcodesLoading = true
                    val result = runCatching {
                        withContext(Dispatchers.IO) {
                            apiClient.deactivateBarcode(warehouseId, currentBarcode.id)
                            apiClient.getCellBarcodes(cell.id)
                        }
                    }
                    result.fold(
                        onSuccess = {
                            barcodes = it
                            barcodesLoading = false
                            barcodesMessage = "Штрих-код деактивирован."
                            barcodeToDeactivate = null
                        },
                        onFailure = { error ->
                            barcodesLoading = false
                            if (error is UnauthorizedException) {
                                onSessionExpired()
                            } else {
                                barcodesMessage = error.message ?: "Не удалось деактивировать штрих-код."
                            }
                        }
                    )
                }
            }
        )
    }
}

@Composable
private fun CellRow(
    cell: CellDto,
    stockCount: Int,
    totalQuantity: Double,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(96.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Spacer(modifier = Modifier.width(16.dp))

            Box(
                modifier = Modifier
                    .width(58.dp)
                    .height(58.dp)
                    .background(Color(0xFFEFF8F3), RoundedCornerShape(12.dp)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.Outlined.Place,
                    contentDescription = null,
                    tint = Color(0xFF2E8B57)
                )
            }

            Spacer(modifier = Modifier.width(14.dp))

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = cell.code,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    text = "${cell.zoneCode} · ${cell.name}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
                Text(
                    text = if (stockCount > 0) {
                        "Позиций: $stockCount · Остаток: ${totalQuantity.formatQuantity()}"
                    } else {
                        "Ячейка сейчас пустая"
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = TextMuted
                )
            }

            Spacer(modifier = Modifier.width(16.dp))
        }
    }
}

@Composable
private fun ScreenHeader(
    title: String,
    onBack: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        IconButton(onClick = onBack) {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                contentDescription = "Назад",
                tint = Color.Black
            )
        }
        Text(
            text = title,
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.weight(1f),
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.width(48.dp))
    }
}

@Composable
private fun BarcodeDeactivateDialog(
    value: String,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Деактивация штрих-кода") },
        text = {
            Text("Штрих-код \"$value\" будет отключён и перестанет использоваться при поиске.")
        },
        confirmButton = {
            Button(onClick = onConfirm) {
                Text("Деактивировать")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Отмена")
            }
        }
    )
}

private fun Double.formatQuantity(): String {
    val rounded = roundToLong().toDouble()
    return if (abs(this - rounded) < 0.000001) {
        rounded.toLong().toString()
    } else {
        String.format(Locale.US, "%.2f", this).trimEnd('0').trimEnd('.')
    }
}
